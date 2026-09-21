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
        private const int Speed = 20;
        private const int BoardSize = 700;

        public static Bullet MoveBullet(Bullet bullet)
        {
            if (bullet.PositionX < 0 || bullet.PositionX > BoardSize || bullet.PositionY < 0 || bullet.PositionY > BoardSize)
            {
                return null;
            }
            double radians = Math.PI * bullet.Angle / 180.0;
            var deltaX = (int)(Speed * Math.Cos(radians));
            var deltaY = (int)(Speed * Math.Sin(radians));
            var newBullet = bullet with
            {
                PositionX = bullet.PositionX + deltaX,
                PositionY = bullet.PositionY + deltaY
            };
            return newBullet;
        }
    }
}
