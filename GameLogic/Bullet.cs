using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameLogic
{
    public record Bullet
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public int PositionX { get; init; }
        public int PositionY { get; init; }
        public int Angle { get; init; }
        private const int Speed = 20;
        public const int BulletSize = 10;

        public static Bullet? MoveBullet(Bullet bullet)
        {
            return MoveBullet(bullet, MapCatalog.DefaultMap);
        }

        public static Bullet? MoveBullet(Bullet bullet, GameMap map)
        {
            if (map.Blocks(new RectangleArea(bullet.PositionX, bullet.PositionY, BulletSize, BulletSize)))
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
            return map.Blocks(new RectangleArea(newBullet.PositionX, newBullet.PositionY, BulletSize, BulletSize))
                ? null
                : newBullet;
        }
    }
}
