
USE [TextileMonitoringDB]
GO

IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Textiles_Name_INCLUDE')
BEGIN
    DROP INDEX [IX_Textiles_Name_INCLUDE] ON [dbo].[Textiles]
END
GO

CREATE NONCLUSTERED INDEX [IX_Textiles_Name_INCLUDE]
ON [dbo].[Textiles]([Name])
INCLUDE (
    [Id], [Dynasty], [Material], [Location], [Status],
    [WidthCm], [HeightCm], [AreaCm2], [ImageUrl], [UpdatedAt]
)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
      DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON,
      ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Textiles_Location_INCLUDE')
BEGIN
    DROP INDEX [IX_Textiles_Location_INCLUDE] ON [dbo].[Textiles]
END
GO

CREATE NONCLUSTERED INDEX [IX_Textiles_Location_INCLUDE]
ON [dbo].[Textiles]([Location])
INCLUDE (
    [Id], [Name], [Dynasty], [Status], [UpdatedAt]
)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
      DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON,
      ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Textiles_Dynasty_Status_INCLUDE')
BEGIN
    DROP INDEX [IX_Textiles_Dynasty_Status_INCLUDE] ON [dbo].[Textiles]
END
GO

CREATE NONCLUSTERED INDEX [IX_Textiles_Dynasty_Status_INCLUDE]
ON [dbo].[Textiles]([Dynasty], [Status])
INCLUDE (
    [Id], [Name], [Location], [Material], [UpdatedAt]
)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
      DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON,
      ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_DustSensorData_TextileId_ReadingTime_INCLUDE')
BEGIN
    DROP INDEX [IX_DustSensorData_TextileId_ReadingTime_INCLUDE] ON [dbo].[DustSensorData]
END
GO

CREATE NONCLUSTERED INDEX [IX_DustSensorData_TextileId_ReadingTime_INCLUDE]
ON [dbo].[DustSensorData]([TextileId], [ReadingTime] DESC)
INCLUDE (
    [Id], [SensorId], [FrassDensity], [HoleCount], [HoleDensity],
    [Temperature], [Humidity]
)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
      DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON,
      ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_FungiSensorData_TextileId_ReadingTime_INCLUDE')
BEGIN
    DROP INDEX [IX_FungiSensorData_TextileId_ReadingTime_INCLUDE] ON [dbo].[FungiSensorData]
END
GO

CREATE NONCLUSTERED INDEX [IX_FungiSensorData_TextileId_ReadingTime_INCLUDE]
ON [dbo].[FungiSensorData]([TextileId], [ReadingTime] DESC)
INCLUDE (
    [Id], [SensorId], [SporeCount], [FungiCFU],
    [Temperature], [Humidity], [DominantFungiType]
)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
      DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON,
      ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Alerts_Resolved_CreatedAt_INCLUDE')
BEGIN
    DROP INDEX [IX_Alerts_Resolved_CreatedAt_INCLUDE] ON [dbo].[Alerts]
END
GO

CREATE NONCLUSTERED INDEX [IX_Alerts_Resolved_CreatedAt_INCLUDE]
ON [dbo].[Alerts]([Resolved], [CreatedAt] DESC)
INCLUDE (
    [Id], [TextileId], [AlertType], [AlertLevel], [Title], [ActualValue], [Threshold]
)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
      DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON,
      ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'FT_TextileCatalog'
)
BEGIN
    CREATE FULLTEXT CATALOG [FT_TextileCatalog]
    WITH ACCENT_SENSITIVITY = OFF
    AS DEFAULT
    AUTHORIZATION [dbo];
END
GO

IF EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.fulltext_indexes fi ON t.object_id = fi.object_id
    WHERE t.name = N'Textiles'
)
BEGIN
    DROP FULLTEXT INDEX ON [dbo].[Textiles];
END
GO

CREATE FULLTEXT INDEX ON [dbo].[Textiles]
(
    [Name] Language 2052,
    [Dynasty] Language 2052,
    [Material] Language 2052,
    [Location] Language 2052,
    [Description] Language 2052
)
KEY INDEX [PK_Textiles]
ON ([FT_TextileCatalog])
WITH (CHANGE_TRACKING = AUTO, STOPLIST = SYSTEM);
GO

PRINT N'索引创建完成！'
PRINT N'已创建覆盖索引：'
PRINT N'  - IX_Textiles_Name_INCLUDE (织绣名称查询覆盖索引)'
PRINT N'  - IX_Textiles_Location_INCLUDE (位置查询覆盖索引)'
PRINT N'  - IX_Textiles_Dynasty_Status_INCLUDE (朝代+状态复合索引)'
PRINT N'  - IX_DustSensorData_TextileId_ReadingTime_INCLUDE'
PRINT N'  - IX_FungiSensorData_TextileId_ReadingTime_INCLUDE'
PRINT N'  - IX_Alerts_Resolved_CreatedAt_INCLUDE'
PRINT N'全文索引：Textiles表 (Name, Dynasty, Material, Location, Description'
GO
