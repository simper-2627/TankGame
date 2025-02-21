using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameLogic
{
    public record Bullet
    {
        public int PositionX { get; init; }
        public int PositionY { get; init; }
        public int Angle { get; init; }
        private const int Speed = 10;
        private const int BoardSize = 700;

        public static Bullet MoveBullet(Bullet bullet)
        {
            double radians = Math.PI * bullet.Angle / 180.0;
            var deltaX = (int)(Speed * Math.Cos(radians));
            var deltaY = (int)(Speed * Math.Sin(radians));
            var newBullet = bullet with
            {
                PositionX = Math.Clamp(bullet.PositionX + deltaX, 0, BoardSize),
                PositionY = Math.Clamp(bullet.PositionY + deltaY, 0, BoardSize)
            };
            return newBullet;
        }
    }
}
