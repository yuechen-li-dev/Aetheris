import Mesh
import math

mesh = Mesh.Mesh(r"C:\Users\yuech\source\repos\Aetheris\docs\preview2\evidence\surface-mesh-ir-m2\through-hole.stl")
box = mesh.BoundBox
cap_centroid_radii = []
for facet in mesh.Facets:
    points = facet.Points
    if max(point[2] for point in points) - min(point[2] for point in points) < 1e-6 and (
        abs(points[0][2] - box.ZMin) < 1e-6 or abs(points[0][2] - box.ZMax) < 1e-6
    ):
        centroid_x = sum(point[0] for point in points) / 3.0
        centroid_y = sum(point[1] for point in points) / 3.0
        cap_centroid_radii.append(math.hypot(centroid_x, centroid_y))
min_cap_centroid_radius = min(cap_centroid_radii)
assert min_cap_centroid_radius > 3.8, "cap triangles entered the radius-4 circular trim"
print("facets=%d points=%d solid=%s volume=%.9f bounds=%.9f,%.9f,%.9f" % (
    mesh.CountFacets, mesh.CountPoints, mesh.isSolid(), mesh.Volume, box.XLength, box.YLength, box.ZLength))
print("capFacets=%d minCapCentroidRadius=%.9f trimClear=True" % (
    len(cap_centroid_radii), min_cap_centroid_radius))
