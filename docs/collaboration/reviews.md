# Firmament engineering reviews

Reviews live in ordinary `.firmament` source and target stable semantics:

```firmament
Review DFM-004 {
    Target: MountDiameter;
    Status: Resolved;

    Issue DFM-004-I1 {
        Author: "Alice Chen";
        Organization: "Northstar Machining";
        Date: 2026-08-10;
        Text: "Current bore tolerance requires finish grinding.";
    }
    Proposal DFM-004-P1 {
        Author: "Alice Chen";
        Date: 2026-08-10;
        Property: tolerance;
        Current: PlusMinus(0.005mm);
        Proposed: PlusMinus(0.010mm);
        Reason: "Avoid a secondary grinding operation.";
    }
    Resolution DFM-004-R1 {
        Author: "Daniel Ruiz";
        Date: 2026-08-10;
        Text: "Accepted as a proposal for the next authoritative revision.";
    }
}
```

Entry kinds are `Comment`, `Issue`, `Proposal`, and `Resolution`. Thread status is one of `Open`, `Accepted`, `Rejected`, `Resolved`, or `Superseded`; status is never inferred from prose. Every entry requires an author name and an explicit ISO `YYYY-MM-DD` authored date. Organization and email are optional.

Structured proposals may retain property, current value, proposed value, units, rationale, author, and date. Prose-only proposals remain valid. Known semantic targets are resolved during Drawing compilation; unknown targets fail. When current and proposed units are explicit and differ, compilation fails. Review order is authored source order.

Git remains version control. The syntax is deliberately stable and diffable: no build-time dates, random identifiers, coordinate targets, or generated ordering.
