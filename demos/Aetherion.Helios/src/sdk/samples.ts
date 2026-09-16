export const bracketSource = `Model WebBracket {
    Units: mm
    Box Plate { Size: [50mm, 40mm, 8mm] }
    Modify Plate {
        Hole<Shaft> CenterMount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll }
    }
}
`;

export const assemblySource = `Units: mm

Template < H: Length > Struct WebBlock {
    Circle2 Circle { Center: [0mm,0mm] Radius: 4mm }
    Profile Section { Loop Outer { Circle |> TraceLoop } }
    Extrude Body { Profile: Section From: 0mm To: H }
}

Assembly WebBlockPair {
    <Assembly WebBlockPair>
        <Part Fixed = WebBlock<H: 10mm>></Part>
        <Part Moving = WebBlock<H: 8mm>>
            Placement ImportedOccurrence = [1,0,0,0, 0,1,0,0, 0,0,1,0, 20,0,0,1];
        </Part>
    </Assembly>
    Anchor: WebBlockPair.Fixed;
}
`;

export const emptyModelSource = `Model Untitled {
    Units: mm
    Box Body { Size: [40mm, 30mm, 8mm] }
}
`;
