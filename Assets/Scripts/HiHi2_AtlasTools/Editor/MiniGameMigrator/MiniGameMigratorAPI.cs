// HiHi2_AtlasTools/Editor/MiniGameMigrator/MiniGameMigratorAPI.cs

using System.Collections.Generic;

namespace HiHi2.AtlasTools.Editor
{
    public static class MiniGameMigratorAPI
    {
        public static List<AvatarObjectInfo> ScanFolder(string folderPath)
        {
            return MiniGameMigratorScanner.ScanFolder(folderPath);
        }

        public static MigrationResult MigrateObject(AvatarObjectInfo objectInfo, MigrationOptions options = null)
        {
            if (options == null)
                options = new MigrationOptions();

            return MiniGameMigratorProcessor.MigrateObject(objectInfo, options);
        }

        public static BatchMigrationResult MigrateBatch(List<AvatarObjectInfo> objectInfos, MigrationOptions options = null)
        {
            if (options == null)
                options = new MigrationOptions();

            return MiniGameMigratorProcessor.MigrateBatch(objectInfos, options);
        }

        public static BatchMigrationResult MigrateFolder(string folderPath, MigrationOptions options = null)
        {
            List<AvatarObjectInfo> objects = ScanFolder(folderPath);
            return MigrateBatch(objects, options);
        }

        public static string GetTargetPath(AvatarObjectInfo objectInfo)
        {
            return MiniGameMigratorScanner.GetTargetPath(objectInfo);
        }
    }
}
