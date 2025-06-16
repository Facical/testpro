using System;
using System.Windows.Media;

namespace testpro.Models
{
    public enum ObjectType
    {
        Shelf,
        Refrigerator,
        Freezer,
        Checkout,
        DisplayStand,
        Pillar
    }

    public class StoreObject
    {
        public string Id { get; private set; }
        public ObjectType Type { get; set; }
        public Point2D Position { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public int Layers { get; set; }
        public bool IsHorizontal { get; set; }
        public Brush Fill { get; set; }
        public Brush Stroke { get; set; }
        public bool IsSelected { get; set; }

        public string ModelBasePath { get; set; }
        public string ShelfModelPath { get; set; }
        public bool HasLayerSupport { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string CategoryCode { get; set; }
        public double Temperature { get; set; }

        public StoreObject(ObjectType type, Point2D position)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            Position = position;
            IsHorizontal = true;
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            CategoryCode = "GEN";

            switch (type)
            {
                case ObjectType.Shelf:
                    Width = 48; Length = 18; Height = 72; Layers = 3;
                    Fill = new SolidColorBrush(Color.FromRgb(139, 69, 19));
                    HasLayerSupport = true;
                    ModelBasePath = "Models/Shelf/ConvenienceShelf.obj";
                    //ShelfModelPath = "Models/Shelf/shelf_layer.obj";
                    break;
                case ObjectType.Refrigerator:
                    Width = 36; Length = 24; Height = 84; Layers = 2;
                    Fill = new SolidColorBrush(Color.FromRgb(200, 200, 255));
                    HasLayerSupport = true;
                    ModelBasePath = "Models/Refrigerator/beverage_refrigerator.obj";
                    ShelfModelPath = "Models/Refrigerator/refrigerator_shelf.obj";
                    Temperature = 4.0;
                    break;
                case ObjectType.Freezer:
                    Width = 36; Length = 24; Height = 84; Layers = 3;
                    Fill = new SolidColorBrush(Color.FromRgb(150, 200, 255));
                    HasLayerSupport = true;
                    ModelBasePath = "Models/Freezer/freezer.obj";
                    Temperature = -18.0;
                    break;
                case ObjectType.Checkout:
                    Width = 48; Length = 36; Height = 36; Layers = 1;
                    Fill = new SolidColorBrush(Color.FromRgb(192, 192, 192));
                    HasLayerSupport = false;
                    ModelBasePath = "Models/Checkout/checkout.obj";
                    break;

                case ObjectType.DisplayStand:
                    Width = 60; Length = 30; Height = 48; Layers = 2;
                    Fill = new SolidColorBrush(Color.FromRgb(255, 228, 196));
                    HasLayerSupport = true;
                    ModelBasePath = "Models/DisplayStand/display_stand_pillar.obj";
                    ShelfModelPath = "Models/DisplayStand/display_shelf.obj";
                    break;
                case ObjectType.Pillar:
                    Width = 12; Length = 12; Height = 96; Layers = 1;
                    Fill = new SolidColorBrush(Color.FromRgb(128, 128, 128));
                    HasLayerSupport = false;
                    ModelBasePath = "Models/Pillar/pillar.obj";
                    break;
            }
            Stroke = Brushes.Black;
        }

        public string GetDisplayName() => Type switch
        {
            ObjectType.Shelf => "선반",
            ObjectType.Refrigerator => "냉장고",
            ObjectType.Freezer => "냉동고",
            ObjectType.Checkout => "계산대",
            ObjectType.DisplayStand => "진열대",
            ObjectType.Pillar => "기둥",
            _ => "객체",
        };

        public (Point2D min, Point2D max) GetBoundingBox()
        {
            double actualWidth = IsHorizontal ? Width : Length;
            double actualLength = IsHorizontal ? Length : Width;
            return (new Point2D(Position.X, Position.Y), new Point2D(Position.X + actualWidth, Position.Y + actualLength));
        }

        public bool ContainsPoint(Point2D point)
        {
            var (min, max) = GetBoundingBox();
            return point.X >= min.X && point.X <= max.X &&
                   point.Y >= min.Y && point.Y <= max.Y;
        }

        public double GetLayerHeight() => Layers > 0 ? Height / Layers : Height;

        public double GetLayerZPosition(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= Layers) return 0;
            return layerIndex * GetLayerHeight();
        }

        public override string ToString()
        {
            return $"{GetDisplayName()} - 위치: ({Position.X:F0}, {Position.Y:F0}), 크기: {Width:F0}x{Length:F0}x{Height:F0}, 층수: {Layers}";
        }
    }
}