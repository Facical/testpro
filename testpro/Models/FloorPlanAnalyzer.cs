using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using testpro.Models;
using System.Windows;
// Point 타입 충돌 해결을 위한 별칭
using DrawingPoint = System.Drawing.Point;

namespace testpro.Models
{
    public class FloorPlanAnalyzer
    {
        public class WallLine
        {
            public Point2D Start { get; set; }
            public Point2D End { get; set; }
            public double Length { get; set; }
            public bool IsHorizontal { get; set; }
            public bool IsVertical { get; set; }
        }

        public class FloorPlanBounds
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        // 기존 메서드 (호환성 유지)
        public List<WallLine> DetectOuterWalls(BitmapImage image)
        {
            var walls = new List<WallLine>();
            return walls;
        }

        // 이미지에서 도면의 실제 경계 찾기
        public FloorPlanBounds FindFloorPlanBounds(BitmapImage image)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(stream);

                    using (var bitmap = new Bitmap(stream))
                    {
                        return FindActualFloorPlanBounds(bitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"도면 경계 찾기 실패: {ex.Message}");
                return null;
            }
        }

        private FloorPlanBounds FindActualFloorPlanBounds(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            // 도면의 외곽선을 찾기 위한 변수
            int minX = width;
            int maxX = 0;
            int minY = height;
            int maxY = 0;

            // 첫 번째 패스: 전체 이미지에서 도면 영역 찾기
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int grayValue = (pixel.R + pixel.G + pixel.B) / 3;

                    if (grayValue < 200)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (minX >= width || minY >= height)
                return null;

            // 두 번째 패스: 도면의 실제 외곽선 찾기
            bool foundLeft = false;
            for (int x = minX; x <= maxX && !foundLeft; x++)
            {
                int darkPixelCount = 0;
                for (int y = minY; y <= maxY; y++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int grayValue = (pixel.R + pixel.G + pixel.B) / 3;
                    if (grayValue < 180)
                        darkPixelCount++;
                }
                if (darkPixelCount > (maxY - minY) * 0.3)
                {
                    minX = x;
                    foundLeft = true;
                }
            }

            bool foundRight = false;
            for (int x = maxX; x >= minX && !foundRight; x--)
            {
                int darkPixelCount = 0;
                for (int y = minY; y <= maxY; y++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int grayValue = (pixel.R + pixel.G + pixel.B) / 3;
                    if (grayValue < 180)
                        darkPixelCount++;
                }
                if (darkPixelCount > (maxY - minY) * 0.3)
                {
                    maxX = x;
                    foundRight = true;
                }
            }

            bool foundTop = false;
            for (int y = minY; y <= maxY && !foundTop; y++)
            {
                int darkPixelCount = 0;
                for (int x = minX; x <= maxX; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int grayValue = (pixel.R + pixel.G + pixel.B) / 3;
                    if (grayValue < 180)
                        darkPixelCount++;
                }
                if (darkPixelCount > (maxX - minX) * 0.3)
                {
                    minY = y;
                    foundTop = true;
                }
            }

            bool foundBottom = false;
            for (int y = maxY; y >= minY && !foundBottom; y--)
            {
                int darkPixelCount = 0;
                for (int x = minX; x <= maxX; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int grayValue = (pixel.R + pixel.G + pixel.B) / 3;
                    if (grayValue < 180)
                        darkPixelCount++;
                }
                if (darkPixelCount > (maxX - minX) * 0.3)
                {
                    maxY = y;
                    foundBottom = true;
                }
            }

            int insetMargin = 2;
            minX = Math.Max(0, minX + insetMargin);
            minY = Math.Max(0, minY + insetMargin);
            maxX = Math.Min(width - 1, maxX - insetMargin);
            maxY = Math.Min(height - 1, maxY - insetMargin);

            System.Diagnostics.Debug.WriteLine($"도면 경계 감지: Left={minX}, Top={minY}, Right={maxX}, Bottom={maxY}");
            System.Diagnostics.Debug.WriteLine($"도면 크기: {maxX - minX} x {maxY - minY}");

            return new FloorPlanBounds
            {
                Left = minX,
                Top = minY,
                Right = maxX,
                Bottom = maxY
            };
        }

        // 개선된 객체 감지 - 외곽선 기반
        public List<DetectedObject> DetectFloorPlanObjects(BitmapImage image, FloorPlanBounds bounds)
        {
            var detectedObjects = new List<DetectedObject>();

            try
            {
                using (var stream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(stream);

                    using (var bitmap = new Bitmap(stream))
                    {
                        // 에지 감지로 외곽선 찾기
                        var edges = DetectEdges(bitmap, bounds);

                        // 외곽선에서 사각형 찾기
                        var rectangles = FindRectangles(edges, bitmap.Width, bitmap.Height, bounds);

                        // 감지된 사각형을 객체로 변환
                        foreach (var rect in rectangles)
                        {
                            var obj = new DetectedObject
                            {
                                Bounds = rect,
                                Type = GuessObjectType(rect),
                                Confidence = 0.8
                            };
                            detectedObjects.Add(obj);
                        }

                        System.Diagnostics.Debug.WriteLine($"감지된 객체 수: {detectedObjects.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"객체 감지 실패: {ex.Message}");
            }

            return detectedObjects;
        }

        // 에지 감지 (Sobel 필터 간소화 버전)
        private bool[,] DetectEdges(Bitmap bitmap, FloorPlanBounds bounds)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            bool[,] edges = new bool[width, height];

            // 더 낮은 임계값으로 연한 선도 감지
            int threshold = 30;

            for (int y = bounds.Top + 1; y < bounds.Bottom - 1; y++)
            {
                for (int x = bounds.Left + 1; x < bounds.Right - 1; x++)
                {
                    // 중심 픽셀
                    Color center = bitmap.GetPixel(x, y);
                    int centerGray = (center.R + center.G + center.B) / 3;

                    // 수평 그래디언트
                    Color left = bitmap.GetPixel(x - 1, y);
                    Color right = bitmap.GetPixel(x + 1, y);
                    int leftGray = (left.R + left.G + left.B) / 3;
                    int rightGray = (right.R + right.G + right.B) / 3;
                    int gx = Math.Abs(rightGray - leftGray);

                    // 수직 그래디언트
                    Color top = bitmap.GetPixel(x, y - 1);
                    Color bottom = bitmap.GetPixel(x, y + 1);
                    int topGray = (top.R + top.G + top.B) / 3;
                    int bottomGray = (bottom.R + bottom.G + bottom.B) / 3;
                    int gy = Math.Abs(bottomGray - topGray);

                    // 에지 강도
                    int magnitude = (int)Math.Sqrt(gx * gx + gy * gy);

                    // 에지 판단
                    edges[x, y] = magnitude > threshold;
                }
            }

            return edges;
        }

        // 사각형 찾기
        private List<Rect> FindRectangles(bool[,] edges, int width, int height, FloorPlanBounds bounds)
        {
            var rectangles = new List<Rect>();
            bool[,] visited = new bool[width, height];

            // 수평선과 수직선 찾기
            var horizontalLines = FindHorizontalLines(edges, bounds);
            var verticalLines = FindVerticalLines(edges, bounds);

            System.Diagnostics.Debug.WriteLine($"수평선: {horizontalLines.Count}개, 수직선: {verticalLines.Count}개");

            // 선들의 교차점에서 사각형 찾기
            for (int i = 0; i < horizontalLines.Count - 1; i++)
            {
                for (int j = i + 1; j < horizontalLines.Count; j++)
                {
                    var topLine = horizontalLines[i];
                    var bottomLine = horizontalLines[j];

                    // 수직 거리 확인 (20~300 픽셀)
                    double vDistance = Math.Abs(bottomLine.Y - topLine.Y);
                    if (vDistance < 20 || vDistance > 300) continue;

                    for (int k = 0; k < verticalLines.Count - 1; k++)
                    {
                        for (int l = k + 1; l < verticalLines.Count; l++)
                        {
                            var leftLine = verticalLines[k];
                            var rightLine = verticalLines[l];

                            // 수평 거리 확인 (20~300 픽셀)
                            double hDistance = Math.Abs(rightLine.X - leftLine.X);
                            if (hDistance < 20 || hDistance > 300) continue;

                            // 선들이 사각형을 형성하는지 확인
                            if (IsRectangle(topLine, bottomLine, leftLine, rightLine))
                            {
                                var rect = new Rect(
                                    leftLine.X,
                                    topLine.Y,
                                    hDistance,
                                    vDistance
                                );

                                // 중복 체크
                                if (!IsOverlapping(rect, rectangles))
                                {
                                    rectangles.Add(rect);
                                }
                            }
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"감지된 사각형: {rectangles.Count}개");
            return rectangles;
        }

        // 수평선 찾기
        private List<(double Y, double StartX, double EndX)> FindHorizontalLines(bool[,] edges, FloorPlanBounds bounds)
        {
            var lines = new List<(double Y, double StartX, double EndX)>();

            for (int y = bounds.Top; y < bounds.Bottom; y += 2)
            {
                int lineStart = -1;
                int consecutivePixels = 0;

                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (edges[x, y])
                    {
                        if (lineStart == -1)
                            lineStart = x;
                        consecutivePixels++;
                    }
                    else
                    {
                        if (consecutivePixels > 30) // 최소 30픽셀 이상의 선
                        {
                            lines.Add((y, lineStart, x - 1));
                        }
                        lineStart = -1;
                        consecutivePixels = 0;
                    }
                }

                // 라인이 끝까지 이어지는 경우
                if (consecutivePixels > 30)
                {
                    lines.Add((y, lineStart, bounds.Right - 1));
                }
            }

            // 가까운 선들 병합
            return MergeCloseLines(lines, true);
        }

        // 수직선 찾기
        private List<(double X, double StartY, double EndY)> FindVerticalLines(bool[,] edges, FloorPlanBounds bounds)
        {
            var lines = new List<(double X, double StartY, double EndY)>();

            for (int x = bounds.Left; x < bounds.Right; x += 2)
            {
                int lineStart = -1;
                int consecutivePixels = 0;

                for (int y = bounds.Top; y < bounds.Bottom; y++)
                {
                    if (edges[x, y])
                    {
                        if (lineStart == -1)
                            lineStart = y;
                        consecutivePixels++;
                    }
                    else
                    {
                        if (consecutivePixels > 30) // 최소 30픽셀 이상의 선
                        {
                            lines.Add((x, lineStart, y - 1));
                        }
                        lineStart = -1;
                        consecutivePixels = 0;
                    }
                }

                // 라인이 끝까지 이어지는 경우
                if (consecutivePixels > 30)
                {
                    lines.Add((x, lineStart, bounds.Bottom - 1));
                }
            }

            // 가까운 선들 병합
            return MergeCloseLines(lines, false);
        }

        // 가까운 선들 병합
        private List<(double Y, double StartX, double EndX)> MergeCloseLines(
            List<(double Y, double StartX, double EndX)> lines, bool isHorizontal)
        {
            if (isHorizontal)
            {
                var merged = new List<(double Y, double StartX, double EndX)>();
                var sorted = lines.OrderBy(l => l.Y).ToList();

                for (int i = 0; i < sorted.Count; i++)
                {
                    var current = sorted[i];
                    double avgY = current.Y;
                    double minX = current.StartX;
                    double maxX = current.EndX;
                    int count = 1;

                    // 5픽셀 이내의 선들 병합
                    while (i + 1 < sorted.Count && Math.Abs(sorted[i + 1].Y - current.Y) < 5)
                    {
                        i++;
                        avgY = (avgY * count + sorted[i].Y) / (count + 1);
                        minX = Math.Min(minX, sorted[i].StartX);
                        maxX = Math.Max(maxX, sorted[i].EndX);
                        count++;
                    }

                    merged.Add((avgY, minX, maxX));
                }

                return merged;
            }
            else
            {
                // 수직선 병합 (비슷한 로직)
                var verticalLines = lines.Select(l => (l.Item1, l.Item2, l.Item3)).ToList();
                var merged = new List<(double X, double StartY, double EndY)>();
                var sorted = verticalLines.OrderBy(l => l.Item1).ToList();

                for (int i = 0; i < sorted.Count; i++)
                {
                    var current = sorted[i];
                    double avgX = current.Item1;
                    double minY = current.Item2;
                    double maxY = current.Item3;
                    int count = 1;

                    while (i + 1 < sorted.Count && Math.Abs(sorted[i + 1].Item1 - current.Item1) < 5)
                    {
                        i++;
                        avgX = (avgX * count + sorted[i].Item1) / (count + 1);
                        minY = Math.Min(minY, sorted[i].Item2);
                        maxY = Math.Max(maxY, sorted[i].Item3);
                        count++;
                    }

                    merged.Add((avgX, minY, maxY));
                }

                return merged.Select(m => (m.Item1, m.Item2, m.Item3)).ToList();
            }
        }

        // 사각형 형성 확인
        private bool IsRectangle(
            (double Y, double StartX, double EndX) topLine,
            (double Y, double StartX, double EndX) bottomLine,
            (double X, double StartY, double EndY) leftLine,
            (double X, double StartY, double EndY) rightLine)
        {
            // 수평선들이 수직선들과 교차하는지 확인
            bool topLeftOK = leftLine.X >= topLine.StartX - 10 && leftLine.X <= topLine.EndX + 10 &&
                            topLine.Y >= leftLine.StartY - 10 && topLine.Y <= leftLine.EndY + 10;

            bool topRightOK = rightLine.X >= topLine.StartX - 10 && rightLine.X <= topLine.EndX + 10 &&
                             topLine.Y >= rightLine.StartY - 10 && topLine.Y <= rightLine.EndY + 10;

            bool bottomLeftOK = leftLine.X >= bottomLine.StartX - 10 && leftLine.X <= bottomLine.EndX + 10 &&
                               bottomLine.Y >= leftLine.StartY - 10 && bottomLine.Y <= leftLine.EndY + 10;

            bool bottomRightOK = rightLine.X >= bottomLine.StartX - 10 && rightLine.X <= bottomLine.EndX + 10 &&
                                bottomLine.Y >= rightLine.StartY - 10 && bottomLine.Y <= rightLine.EndY + 10;

            return topLeftOK && topRightOK && bottomLeftOK && bottomRightOK;
        }

        // 객체 타입 추측
        private DetectedObjectType GuessObjectType(Rect bounds)
        {
            double ratio = bounds.Width / bounds.Height;
            double area = bounds.Width * bounds.Height;

            // 크기와 비율로 타입 추측
            if (area < 1000)
            {
                return DetectedObjectType.Pillar; // 작은 객체는 기둥
            }
            else if (ratio > 2.5 || ratio < 0.4)
            {
                // 매우 길쭉한 형태
                return area > 3000 ? DetectedObjectType.DisplayStand : DetectedObjectType.Shelf;
            }
            else if (area > 6000)
            {
                // 큰 정사각형 형태
                return DetectedObjectType.Checkout;
            }
            else if (area > 3000)
            {
                // 중간 크기
                return DetectedObjectType.Refrigerator;
            }
            else
            {
                // 기본값
                return DetectedObjectType.Shelf;
            }
        }

        // 겹침 확인
        private bool IsOverlapping(Rect newRect, List<Rect> existingRects)
        {
            foreach (var existing in existingRects)
            {
                var intersection = Rect.Intersect(newRect, existing);
                if (!intersection.IsEmpty)
                {
                    double overlapArea = intersection.Width * intersection.Height;
                    double newArea = newRect.Width * newRect.Height;
                    double existingArea = existing.Width * existing.Height;

                    // 70% 이상 겹치면 중복
                    if (overlapArea / Math.Min(newArea, existingArea) > 0.7)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}