"""Aetheris CAD: exact Firmament parts in Blender, with local compiler transport."""
import base64
from pathlib import Path
import time

import bpy
import bmesh
from bpy.app.handlers import persistent
from bpy.props import BoolProperty, CollectionProperty, IntProperty, PointerProperty, StringProperty
from bpy_extras.io_utils import ExportHelper, ImportHelper

from .bridge import BuildError, BuildJob
from . import state

_jobs = {}


class AETHERIS_Resource(bpy.types.PropertyGroup):
    relative_path: StringProperty(name="Project path")
    text: PointerProperty(type=bpy.types.Text)
    binary: BoolProperty(default=False)


class AETHERIS_Part(bpy.types.PropertyGroup):
    is_part: BoolProperty(default=False)
    uid: StringProperty()
    source: PointerProperty(name="Firmament source", type=bpy.types.Text)
    main_file: StringProperty(name="Main file", default="part.firmament")
    resources: CollectionProperty(type=AETHERIS_Resource)
    import_directory: StringProperty(subtype="DIR_PATH")
    step: PointerProperty(type=bpy.types.Text)
    diagnostics: PointerProperty(type=bpy.types.Text)
    accepted_hash: StringProperty()
    step_hash: StringProperty()
    revision: IntProperty(default=0)
    status: StringProperty(default="Ready to build")


class AETHERIS_Preferences(bpy.types.AddonPreferences):
    bl_idname = __package__
    executable_path: StringProperty(name="Compiler override", description="Leave empty to use the bundled local runtime", subtype="FILE_PATH")

    def draw(self, context):
        self.layout.label(text="The complete extension ZIP includes its local Aetheris runtime.")
        self.layout.prop(self, "executable_path")
        self.layout.label(text="Developer override accepts aetheris.exe or aetheris.dll.")


def executable(context):
    addon = context.preferences.addons.get(__package__)
    override = addon.preferences.executable_path if addon else ""
    return Path(bpy.path.abspath(override)).resolve() if override else Path(__file__).parent / "runtime" / "aetheris.exe"


def show_text(context, text):
    if not text or not context.screen:
        return
    area = next((a for a in context.screen.areas if a.type == "TEXT_EDITOR"), None)
    if area is None:
        current = context.area
        if not current or current.type != "VIEW_3D":
            return
        before = {a.as_pointer() for a in context.screen.areas}
        with context.temp_override(area=current):
            bpy.ops.screen.area_split(direction="VERTICAL", factor=.5)
        area = next((a for a in context.screen.areas if a.as_pointer() not in before), None)
        if area is None:
            return
        area.type = "TEXT_EDITOR"
    area.spaces.active.text = text
    area.spaces.active.show_line_numbers = True
    area.spaces.active.show_word_wrap = True
    text.cursor_set(0)
    area.spaces.active.top = 0


def fit_part(context, root):
    body = state.body_of(root)
    if not body or not context.screen:
        return
    for selected in context.selected_objects:
        selected.select_set(False)
    body.select_set(True)
    context.view_layer.objects.active = body
    for area in context.screen.areas:
        if area.type == "VIEW_3D":
            region = next((r for r in area.regions if r.type == "WINDOW"), None)
            if region:
                with context.temp_override(area=area, region=region):
                    bpy.ops.view3d.view_selected(use_all_regions=False)


class AETHERIS_OT_fit(bpy.types.Operator):
    bl_idname = "aetheris.fit_part"
    bl_label = "Fit CAD Part"

    def execute(self, context):
        root = state.find_part(context)
        if not root:
            return {"CANCELLED"}
        fit_part(context, root)
        return {"FINISHED"}


class AETHERIS_OT_new(bpy.types.Operator):
    bl_idname = "aetheris.new_part"
    bl_label = "New CAD Part"
    bl_description = "Create a Firmament source and an exact CAD part in this scene"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        root = state.new_part(context)
        if not bpy.app.background:
            show_text(context, root.aetheris.source)
        return {"FINISHED"}


class AETHERIS_OT_import(bpy.types.Operator, ImportHelper):
    bl_idname = "aetheris.import_source"
    bl_label = "Open Firmament Part"
    bl_description = "Copy a .firmament document into this .blend; add its dependencies explicitly"
    bl_options = {"REGISTER", "UNDO"}
    filename_ext = ".firmament"
    filter_glob: StringProperty(default="*.firmament", options={"HIDDEN"})

    def execute(self, context):
        try:
            path = Path(self.filepath)
            root = state.new_part(context, path.read_text(encoding="utf-8-sig"), path.name, str(path.parent))
            if not bpy.app.background:
                show_text(context, root.aetheris.source)
            return {"FINISHED"}
        except (OSError, UnicodeError) as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class AETHERIS_OT_source(bpy.types.Operator):
    bl_idname = "aetheris.open_source"
    bl_label = "Open Source"
    diagnostic: BoolProperty(default=False, options={"SKIP_SAVE"})

    def execute(self, context):
        root = state.find_part(context)
        if not root:
            return {"CANCELLED"}
        show_text(context, root.aetheris.diagnostics if self.diagnostic else root.aetheris.source)
        return {"FINISHED"}


class AETHERIS_OT_resource(bpy.types.Operator, ImportHelper):
    bl_idname = "aetheris.add_resource"
    bl_label = "Add Project File"
    bl_description = "Pack an explicitly selected include, STEP asset, or lock file into this .blend"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        root = state.find_part(context)
        if not root:
            return {"CANCELLED"}
        try:
            path = Path(self.filepath).resolve()
            data = path.read_bytes()
            if len(data) > 32 * 1024 * 1024:
                raise BuildError("A project file may be at most 32 MiB.")
            directory = root.aetheris.import_directory
            name = path.relative_to(Path(directory).resolve()).as_posix() if directory and path.is_relative_to(Path(directory).resolve()) else path.name
            if name.casefold() in {root.aetheris.main_file.casefold(), *(r.relative_path.casefold() for r in root.aetheris.resources)}:
                raise BuildError("This project already contains " + name)
            try:
                content = data.decode("utf-8")
                binary = False
            except UnicodeError:
                content, binary = base64.b64encode(data).decode("ascii"), True
            text = state.text_block(name, content)
            resource = root.aetheris.resources.add()
            resource.relative_path, resource.text, resource.binary = name, text, binary
            return {"FINISHED"}
        except (OSError, ValueError, BuildError) as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class AETHERIS_OT_remove_resource(bpy.types.Operator):
    bl_idname = "aetheris.remove_resource"
    bl_label = "Unlink Project File"
    bl_options = {"REGISTER", "UNDO"}
    index: IntProperty()

    def execute(self, context):
        root = state.find_part(context)
        if root and 0 <= self.index < len(root.aetheris.resources):
            root.aetheris.resources.remove(self.index)
            return {"FINISHED"}
        return {"CANCELLED"}


class AETHERIS_OT_build(bpy.types.Operator):
    bl_idname = "aetheris.build"
    bl_label = "Build CAD Part"
    bl_description = "Compile the saved source snapshot locally; retain accepted geometry on failure"
    bl_options = {"REGISTER", "UNDO"}
    replace_edited: BoolProperty(default=False, options={"SKIP_SAVE"})
    _timer = None
    _job = None
    _uid = ""

    @classmethod
    def poll(cls, context):
        root = state.find_part(context)
        return root is not None and context.mode == "OBJECT" and root.aetheris.uid not in _jobs

    def execute(self, context):
        root = state.find_part(context)
        self._uid = root.aetheris.uid
        try:
            if state.mesh_edited(root) and not self.replace_edited:
                raise BuildError("The generated mesh has vertex edits. Create a Mesh Copy to retain them, then use Restore CAD Mesh.")
            files, main = state.project_files(root)
            self._job = BuildJob(executable(context), files, main)
            _jobs[self._uid] = self
            root.aetheris.status = "Building locally… Esc cancels"
            if bpy.app.background:
                while self._job.poll() is None:
                    time.sleep(.02)
                return self.finish(context)
            self._timer = context.window_manager.event_timer_add(.1, window=context.window)
            context.window_manager.modal_handler_add(self)
            return {"RUNNING_MODAL"}
        except Exception as error:
            state.diagnostic(root, str(error))
            self.cleanup(context)
            self.report({"WARNING"}, "Build refused. Open Diagnostics for the compiler message.")
            return {"CANCELLED"}

    def modal(self, context, event):
        if event.type == "ESC":
            root = state.find_part(context, self._uid)
            if root:
                root.aetheris.status = "Build cancelled · last accepted geometry retained"
            self.cleanup(context)
            return {"CANCELLED"}
        if event.type == "TIMER":
            try:
                if self._job.poll() is not None:
                    return self.finish(context)
            except Exception as error:
                root = state.find_part(context, self._uid)
                if root:
                    state.diagnostic(root, str(error))
                self.cleanup(context)
                return {"CANCELLED"}
        return {"PASS_THROUGH"}

    def finish(self, context):
        try:
            root = state.find_part(context, self._uid)
            if not root:
                raise BuildError("The target CAD part was removed or duplicated while building.")
            mesh, step = self._job.result()
            first = root.aetheris.revision == 0
            state.install_result(root, mesh, step, self._job.digest, context.scene)
            if first and not bpy.app.background:
                fit_part(context, root)
            self.report({"INFO"}, root.aetheris.status)
            return {"FINISHED"}
        except Exception as error:
            root = state.find_part(context, self._uid)
            if root:
                state.diagnostic(root, str(error))
            self.report({"WARNING"}, "Build refused. Last accepted geometry retained; open Diagnostics.")
            return {"CANCELLED"}
        finally:
            self.cleanup(context)

    def cleanup(self, context):
        if self._job:
            self._job.close()
            self._job = None
        if self._timer:
            context.window_manager.event_timer_remove(self._timer)
            self._timer = None
        _jobs.pop(self._uid, None)
        if context.screen:
            for area in context.screen.areas:
                area.tag_redraw()

    def cancel(self, context):
        self.cleanup(context)


class AETHERIS_OT_step(bpy.types.Operator, ExportHelper):
    bl_idname = "aetheris.export_step"
    bl_label = "Export Exact STEP (Source Frame)"
    bl_description = "Export the accepted source BRep; Blender placement, modifiers, and mesh copies are presentation only"
    filename_ext = ".step"
    filter_glob: StringProperty(default="*.step;*.stp", options={"HIDDEN"})

    @classmethod
    def poll(cls, context):
        root = state.find_part(context)
        return root is not None and bool(root.aetheris.step) and root.aetheris.uid not in _jobs

    def execute(self, context):
        try:
            root = state.find_part(context)
            data = state.accepted_step(root)
            Path(self.filepath).write_text(data, encoding="utf-8", newline="")
            self.report({"INFO"}, "Exact STEP exported in Firmament source coordinates")
            return {"FINISHED"}
        except (OSError, BuildError) as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class AETHERIS_OT_bake(bpy.types.Operator):
    bl_idname = "aetheris.mesh_copy"
    bl_label = "Create Editable Mesh Copy"
    bl_description = "Make an independent Blender mesh for sculpting, booleans, or vertex editing"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        root = state.find_part(context)
        return root is not None and state.body_of(root) is not None and root.aetheris.uid not in _jobs

    def execute(self, context):
        root = state.find_part(context)
        body = state.body_of(root)
        obj = body.copy()
        obj.data = body.data.copy()
        # Display faces retain split vertices for exact normals. Weld only the
        # independent presentation copy so Blender mesh operations get shared seams.
        editable = bmesh.new()
        try:
            editable.from_mesh(obj.data)
            bmesh.ops.remove_doubles(editable, verts=list(editable.verts), dist=1e-8 / context.scene.unit_settings.scale_length)
            editable.normal_update()
            editable.to_mesh(obj.data)
            obj.data.update()
        finally:
            editable.free()
        obj.name = root.name + " Mesh Copy"
        transform = body.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = transform
        for key in ("aetheris_owner", "aetheris_mesh_hash"):
            if key in obj:
                del obj[key]
        context.collection.objects.link(obj)
        for selected in context.selected_objects:
            selected.select_set(False)
        obj.select_set(True)
        context.view_layer.objects.active = obj
        return {"FINISHED"}


def draw_part(layout, context):
    root = state.find_part(context)
    if not root:
        layout.operator("aetheris.new_part", icon="ADD")
        layout.operator("aetheris.import_source", icon="FILE_FOLDER")
        layout.label(text="Select a CAD part or its source to rebuild.")
        return
    part = root.aetheris
    busy = part.uid in _jobs
    layout.label(text=root.name, icon="MESH_CUBE")
    column = layout.column()
    column.enabled = not busy
    column.prop(part, "source")
    column.prop(part, "main_file")
    column.operator("aetheris.open_source", icon="TEXT")
    column.operator("aetheris.fit_part", icon="VIEWZOOM")
    row = column.row()
    row.enabled = context.mode == "OBJECT"
    row.operator("aetheris.build", icon="FILE_REFRESH")
    try:
        stale = state.current_hash(root) != part.accepted_hash
        edited = part.status.startswith("The generated mesh has vertex edits")
    except BuildError:
        stale, edited = True, False
    layout.label(text="Building locally…" if busy else "Source changed · rebuild required" if stale else "Source matches accepted CAD", icon="TIME" if busy else "ERROR" if stale else "CHECKMARK")
    if edited:
        box = layout.box()
        box.alert = True
        box.label(text="Generated mesh has vertex edits")
        box.operator("aetheris.mesh_copy")
        box.operator("aetheris.build", text="Restore CAD Mesh").replace_edited = True
    layout.label(text=part.status[:90])
    if part.diagnostics and part.diagnostics.as_string():
        box = layout.box()
        box.alert = True
        box.label(text="Build refused · accepted part retained", icon="ERROR")
        box.operator("aetheris.open_source", text="Open Diagnostics").diagnostic = True
    column = layout.column()
    column.enabled = not busy
    column.operator("aetheris.mesh_copy", icon="DUPLICATE")
    row = column.row()
    row.enabled = not stale
    row.operator("aetheris.export_step", text="Export Exact STEP", icon="EXPORT")
    layout.label(text="STEP uses source coordinates")
    box = layout.box()
    box.label(text="Project files · saved inside .blend")
    for i, resource in enumerate(part.resources):
        row = box.row(align=True)
        row.enabled = not busy
        row.prop(resource, "relative_path", text="")
        row.operator("aetheris.remove_resource", text="", icon="X").index = i
    row = box.row()
    row.enabled = not busy
    row.operator("aetheris.add_resource", icon="FILE_NEW")


class AETHERIS_PT_view(bpy.types.Panel):
    bl_label = "Aetheris CAD"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Aetheris"

    def draw(self, context):
        draw_part(self.layout, context)


class AETHERIS_PT_text(bpy.types.Panel):
    bl_label = "Aetheris CAD"
    bl_space_type = "TEXT_EDITOR"
    bl_region_type = "UI"
    bl_category = "Aetheris"

    def draw(self, context):
        draw_part(self.layout, context)


def draw_add(self, context):
    self.layout.operator("aetheris.new_part", text="Aetheris CAD Part", icon="MESH_CUBE")


def draw_text_header(self, context):
    root = state.find_part(context)
    if root:
        self.layout.operator("aetheris.build", text="Build CAD", icon="FILE_REFRESH")
        self.layout.operator("aetheris.export_step", text="STEP", icon="EXPORT")


@persistent
def loaded(_):
    for operator in list(_jobs.values()):
        operator.cleanup(bpy.context)
    for obj in bpy.data.objects:
        if obj.aetheris.is_part and obj.aetheris.status.startswith("Building"):
            obj.aetheris.status = "Build interrupted · rebuild to continue"


_classes = (AETHERIS_Resource, AETHERIS_Part, AETHERIS_Preferences, AETHERIS_OT_fit, AETHERIS_OT_new, AETHERIS_OT_import,
            AETHERIS_OT_source, AETHERIS_OT_resource, AETHERIS_OT_remove_resource, AETHERIS_OT_build,
            AETHERIS_OT_step, AETHERIS_OT_bake, AETHERIS_PT_view, AETHERIS_PT_text)


def register():
    for cls in _classes:
        bpy.utils.register_class(cls)
    bpy.types.Object.aetheris = PointerProperty(type=AETHERIS_Part)
    bpy.types.VIEW3D_MT_add.append(draw_add)
    bpy.types.TEXT_HT_header.append(draw_text_header)
    bpy.app.handlers.load_pre.append(loaded)
    bpy.app.handlers.load_post.append(loaded)


def unregister():
    for operator in list(_jobs.values()):
        operator.cleanup(bpy.context)
    bpy.app.handlers.load_pre.remove(loaded)
    bpy.app.handlers.load_post.remove(loaded)
    bpy.types.TEXT_HT_header.remove(draw_text_header)
    bpy.types.VIEW3D_MT_add.remove(draw_add)
    del bpy.types.Object.aetheris
    for cls in reversed(_classes):
        bpy.utils.unregister_class(cls)
