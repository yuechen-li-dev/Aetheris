CREATE TABLE Materials (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Grade TEXT NOT NULL,
    IsAluminum INTEGER NOT NULL
);
CREATE UNIQUE INDEX IX_Materials_Grade ON Materials (Grade);

CREATE TABLE DrawingMetadata (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Company TEXT NOT NULL,
    Author TEXT NOT NULL,
    Description TEXT NOT NULL
);

CREATE TABLE BearingBlockConfigurations (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    PartNumber TEXT NOT NULL,
    WidthMillimeters REAL NOT NULL,
    HeightMillimeters REAL NOT NULL,
    DepthMillimeters REAL NOT NULL,
    BoreDiameterMillimeters REAL NOT NULL,
    BoreTolerancePlusMillimeters REAL NOT NULL,
    BoreToleranceMinusMillimeters REAL NOT NULL,
    RevisionMajor INTEGER NOT NULL,
    RevisionMinor INTEGER NOT NULL,
    RevisionPatch INTEGER NOT NULL,
    IsProduction INTEGER NOT NULL,
    IsCurrent INTEGER NOT NULL,
    MaterialId INTEGER NOT NULL REFERENCES Materials (Id) ON DELETE CASCADE,
    DrawingMetadataId INTEGER NOT NULL REFERENCES DrawingMetadata (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_BearingBlockConfigurations_PartNumber ON BearingBlockConfigurations (PartNumber);
