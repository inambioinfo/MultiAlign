#region

using System;
using MultiAlignCore.Algorithms.Options;
using MultiAlignCore.Data.MassTags;
using MultiAlignCore.IO.InputFiles;

#endregion

namespace MultiAlignCore.IO.MTDB
{
    /// <summary>
    ///     Class that loads Mass Tag databases from a given source.
    /// </summary>
    public static class MtdbLoaderFactory
    {
        /// <summary>
        ///     Loads a mass tag database.
        /// </summary>
        /// <param name="databaseDefinition"></param>
        /// <param name="options">Loading options.</param>
        /// <returns>The mass tag database.</returns>
        public static MassTagDatabase LoadMassTagDatabase(InputDatabase databaseDefinition,
            MassTagDatabaseOptions options)
        {
            var loader = Create(databaseDefinition, options);
            if (loader == null)
            {
                throw new NullReferenceException("The type of mass tag database format is not supported: " +
                                                 databaseDefinition.DatabaseFormat);
            }
            return loader.LoadDatabase();
        }

        /// <summary>
        ///     Loads a mass tag database.
        /// </summary>
        /// <param name="databaseDefinition"></param>
        /// <param name="options">Loading options.</param>
        /// <returns>The mass tag database.</returns>
        public static IMtdbLoader Create(InputDatabase databaseDefinition, MassTagDatabaseOptions options)
        {
            IMtdbLoader loader = null;

            switch (databaseDefinition.DatabaseFormat)
            {
                case MassTagDatabaseFormat.SkipAlignment:
                    loader = new DriftTimeTextFileDatabaseLoader(databaseDefinition.LocalPath);
                    break;
                case MassTagDatabaseFormat.MassTagSystemSql:
                    loader = new MtsMassTagDatabaseLoader(databaseDefinition.DatabaseName,
                        databaseDefinition.DatabaseServer, options);
                    break;
                case MassTagDatabaseFormat.Sqlite:
                    loader = new SQLiteMassTagDatabaseLoader(databaseDefinition.LocalPath, options);
                    break;
                case MassTagDatabaseFormat.MtdbCreator:
                    loader = new MtdbCreatorDatabaseLoader(databaseDefinition.LocalPath);
                    break;
                case MassTagDatabaseFormat.DelimitedTextFile:
                    loader = new MetaSampleDatbaseLoader(databaseDefinition.LocalPath, options);
                    break;
                case MassTagDatabaseFormat.LiquidResultsFile:
                    loader = new LiquidResultsFileLoader(databaseDefinition.LocalPath);
                    break;
                case MassTagDatabaseFormat.GenericTsvFile:
                    loader = new MtdbFromGenericTsvReader(databaseDefinition.LocalPath);
                    break;
            }
            return loader;
        }
    }
}