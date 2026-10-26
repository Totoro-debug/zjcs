using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    public static class OfficialMapNavigationView
    {
        public static bool TryGetCell(OfficialMapNavigation navigation, int x, int y, out int area, out int groundType)
        {
            area = -1;
            groundType = 0;
            if (navigation == null || x < 0 || y < 0 || x >= navigation.Width || y >= navigation.Height
                || navigation.Rows == null || y >= navigation.Rows.Length)
                return false;

            OfficialMapGridRow row = navigation.Rows[y];
            if (row == null || row.Cells == null || x >= row.Cells.Length)
                return false;

            int[] rawValues = row.Cells[x]?.RawValues;
            if (rawValues == null || rawValues.Length != 2)
                return false;

            area = rawValues[0];
            groundType = rawValues[1];
            return true;
        }

        public static OfficialMapVector3 GridToWorld(OfficialMapNavigation navigation, int x, int y)
        {
            return new OfficialMapVector3
            {
                X = navigation.Origin.X + x,
                Y = navigation.Origin.Y + y,
                Z = navigation.Origin.Z
            };
        }

        public static void WorldToGrid(OfficialMapNavigation navigation, OfficialMapVector3 position, out int x, out int y)
        {
            x = (int)(position.X - navigation.Origin.X + 0.5f);
            y = (int)(position.Y - navigation.Origin.Y + 0.5f);
        }
    }
}
