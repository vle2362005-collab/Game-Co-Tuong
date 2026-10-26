using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;

namespace GameCoTuong.Engine.Model;

public static class BoardGeometry
{
    public const int TotalColumns = 9; // 0..8
    public const int TotalRows = 10;   // 0..9
    public const int TotalIntersections = 90;

    public const int RiverRowBlackSide = 4;
    public const int RiverRowRedSide = 5;

    public const int PalaceMinCol = 3;
    public const int PalaceMaxCol = 5;
    public const int BlackPalaceMinRow = 0;
    public const int BlackPalaceMaxRow = 2;
    public const int RedPalaceMinRow = 7;
    public const int RedPalaceMaxRow = 9;

    public static bool IsInPalace(Side side, Position pos)
    {
        if (pos.Col is < PalaceMinCol or > PalaceMaxCol)
            return false;

        return side switch
        {
            Side.Black => pos.Row is >= BlackPalaceMinRow and <= BlackPalaceMaxRow,
            Side.Red => pos.Row is >= RedPalaceMinRow and <= RedPalaceMaxRow,
            _ => false
        };
    }

    public static bool HasCrossedRiver(Side side, Position pos) =>
        side switch
        {
            Side.Black => pos.Row >= RiverRowRedSide, // Đen qua sông khi hàng >= 5
            Side.Red => pos.Row <= RiverRowBlackSide,  // Đỏ qua sông khi hàng <= 4
            _ => false
        };
}
