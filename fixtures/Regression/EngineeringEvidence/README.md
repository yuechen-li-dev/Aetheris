# Engineering evidence witness

These are synthetic qualification inputs, not manufacturer specifications or
supplier offers. Upload requirements.md and catalog.csv through Helios Files →
Engineering references. Ask the agent to filter boreMm ≥ 8 and widthMm ≤ 60,
bind widthMm to width in mm, and prepare a draft review package.

The selected synthetic row is M23, width 56 mm and bore 8 mm. plate.firmament is
the corresponding source witness: 56 × 56 × 6 mm with an 8 mm through hole.
Expected bounds are X/Y [-28, 28] mm and Z [0, 6] mm. The exact volume is
18,816 − 96π mm³. The source remains the geometry authority.

The automated host tests also generate real PDF, DOCX and XLSX inputs in memory;
formula cells reject parameter binding. Detailed local qualification logs belong
under artifacts/local, not this fixture directory.
