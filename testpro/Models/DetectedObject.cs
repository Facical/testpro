using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace testpro.Models
{
    public enum DetectedObjectType
    {
        Unknown,
        Wall,           // 벽
        Refrigerator,   // 냉장고
        Freezer,        // 냉동고 (추가)
        Shelf,          // 선반
        Chair,          // 의자
        Desk,           // 책상
        Microwave,      // 전자레인지
        Door,           // 문
        Window,         // 창문
        Checkout,       // 계산대
        DisplayStand,   // 진열대
        Pillar          // 기둥
    }

    public class DetectedObject
    {
        public string Id { get; set; }
        public DetectedObjectType Type { get; set; }
        public Rect Bounds { get; set; }
        public List<Point> Points { get; set; }
        public bool IsSelected { get; set; }
        public bool IsLine { get; set; }
        public double Confidence { get; set; }

        // UI 요소
        public Shape OverlayShape { get; set; }
        public Shape SelectionBorder { get; set; }

        // 추가: 변환된 StoreObject 참조
        public StoreObject ConvertedStoreObject { get; set; }

        // 추가: 호버 상태
        public bool IsHovered { get; set; }

        public DetectedObject()
        {
            Id = Guid.NewGuid().ToString();
            Points = new List<Point>();
            Type = DetectedObjectType.Unknown;
            Confidence = 0.0;
        }

        public string GetTypeName()
        {
            switch (Type)
            {
                case DetectedObjectType.Wall: return "벽";
                case DetectedObjectType.Refrigerator: return "냉장고";
                case DetectedObjectType.Freezer: return "냉동고";
                case DetectedObjectType.Shelf: return "선반";
                case DetectedObjectType.Chair: return "의자";
                case DetectedObjectType.Desk: return "책상";
                case DetectedObjectType.Microwave: return "전자레인지";
                case DetectedObjectType.Door: return "문";
                case DetectedObjectType.Window: return "창문";
                case DetectedObjectType.Checkout: return "계산대";
                case DetectedObjectType.DisplayStand: return "진열대";
                case DetectedObjectType.Pillar: return "기둥";
                default: return "미지정";
            }
        }

        // StoreObject로 변환 (개선된 버전)
        public StoreObject ToStoreObjectWithProperties(double width, double height, double length,
            int layers, bool isHorizontal, double temperature = 0, string categoryCode = "GEN")
        {
            ObjectType storeType = ObjectType.Shelf; // 기본값

            switch (Type)
            {
                case DetectedObjectType.Shelf:
                    storeType = ObjectType.Shelf;
                    break;
                case DetectedObjectType.Refrigerator:
                    storeType = ObjectType.Refrigerator;
                    break;
                case DetectedObjectType.Freezer:
                    storeType = ObjectType.Freezer;
                    break;
                case DetectedObjectType.Checkout:
                    storeType = ObjectType.Checkout;
                    break;
                case DetectedObjectType.DisplayStand:
                    storeType = ObjectType.DisplayStand;
                    break;
                case DetectedObjectType.Pillar:
                    storeType = ObjectType.Pillar;
                    break;
            }

            var position = new Point2D(Bounds.Left, Bounds.Top);
            var obj = new StoreObject(storeType, position)
            {
                Width = width,
                Length = length,
                Height = height,
                Layers = layers,
                IsHorizontal = isHorizontal,
                CategoryCode = categoryCode
            };

            // 온도 설정 (냉장고/냉동고)
            if (storeType == ObjectType.Refrigerator || storeType == ObjectType.Freezer)
            {
                obj.Temperature = temperature;
            }

            return obj;
        }

        // 기존 메서드 유지 (호환성)
        public StoreObject ToStoreObject()
        {
            ObjectType storeType = ObjectType.Shelf; // 기본값

            switch (Type)
            {
                case DetectedObjectType.Shelf:
                    storeType = ObjectType.Shelf;
                    break;
                case DetectedObjectType.Refrigerator:
                    storeType = ObjectType.Refrigerator;
                    break;
                case DetectedObjectType.Freezer:
                    storeType = ObjectType.Freezer;
                    break;
                case DetectedObjectType.Checkout:
                    storeType = ObjectType.Checkout;
                    break;
                case DetectedObjectType.DisplayStand:
                    storeType = ObjectType.DisplayStand;
                    break;
                case DetectedObjectType.Pillar:
                    storeType = ObjectType.Pillar;
                    break;
            }

            var position = new Point2D(Bounds.Left, Bounds.Top);
            var obj = new StoreObject(storeType, position)
            {
                Width = Bounds.Width,
                Length = Bounds.Height
            };

            return obj;
        }
    }
}