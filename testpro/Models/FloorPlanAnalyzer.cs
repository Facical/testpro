using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Drawing; // System.Drawing.dll 참조 필요
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
        // LineSegment struct 정의: 튜플을 대체할 데이터 컨테이너
        private struct LineSegment
        {
            public double Coordinate { get; set; } // Y 또는 X 좌표 (메인 축)
            public double Start { get; set; }     // 시작 범위 (보조 축)
            public double End { get; set; }       // 끝 범위 (보조 축)

            public LineSegment(double coordinate, double start, double end)
            {
                Coordinate = coordinate;
                Start = start;
                End = end;
            }
        }

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

        public List<DetectedObject> AnalyzeFloorPlan(System.Drawing.Bitmap bitmap)
        {
            var detectedObjects = new List<DetectedObject>();

            // 이 메서드는 더 이상 사용되지 않을 수 있습니다. DetectFloorPlanObjects로 대체.
            // 필요하다면 이곳에 더미 객체 외에 실제 로직을 추가해야 합니다.
            detectedObjects.Add(new DetectedObject
            {
                Type = testpro.Models.DetectedObjectType.Refrigerator,  // Models.DetectedObjectType 사용
                Bounds = new Rect(100, 100, 48, 36),
                Confidence = 0.95
            });

            return detectedObjects;
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
                        var rawRectangles = FindRectangles(edges, bitmap.Width, bitmap.Height, bounds);

                        // 감지된 사각형을 객체로 변환하기 전에 그룹화 처리
                        var groupedRectangles = GroupAdjacentRectangles(rawRectangles);

                        // 감지된 사각형을 객체로 변환
                        foreach (var rect in groupedRectangles)
                        {
                            var obj = new DetectedObject
                            {
                                Bounds = rect,
                                Type = GuessObjectType(rect), // 명확한 DetectedObjectType 사용
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

            // 더 낮은 임계값으로 연한 선도 감지 - 테스트를 위해 임계값 조정
            int threshold = 20; // 30에서 20으로 낮춰서 더 많은 에지를 잡도록 시도

            for (int y = bounds.Top + 1; y < bounds.Bottom - 1; y++)
            {
                for (int x = bounds.Left + 1; x < bounds.Right - 1; x++)
                {
                    // 픽셀이 범위 내에 있는지 확인
                    if (x < 0 || x >= width || y < 0 || y >= height) continue;

                    // 중심 픽셀
                    Color center = bitmap.GetPixel(x, y);
                    int centerGray = (center.R + center.G + center.B) / 3;

                    // 주변 픽셀이 유효 범위 내에 있는지 확인
                    if (x - 1 < 0 || x + 1 >= width || y - 1 < 0 || y + 1 >= height) continue;


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
            // bool[,] visited = new bool[width, height]; // BFS/DFS를 사용하지 않는 경우 불필요

            // 수평선과 수직선 찾기
            List<LineSegment> horizontalLines = FindHorizontalLines(edges, bounds); // LineSegment 반환
            List<LineSegment> verticalLines = FindVerticalLines(edges, bounds);   // LineSegment 반환

            System.Diagnostics.Debug.WriteLine($"수평선: {horizontalLines.Count}개, 수직선: {verticalLines.Count}개");

            // 선들의 교차점에서 사각형 찾기
            // 최적화를 위해 min/max X/Y를 사용하여 검색 범위를 줄입니다.
            // 모든 조합 대신, 겹치는 범위의 선들만 고려
            foreach (var topLine in horizontalLines.OrderBy(l => l.Coordinate)) // LineSegment의 Coordinate 사용
            {
                foreach (var bottomLine in horizontalLines.OrderBy(l => l.Coordinate)) // LineSegment의 Coordinate 사용
                {
                    if (bottomLine.Coordinate <= topLine.Coordinate) continue; // 상단 라인보다 아래에 있어야 함

                    // 수직 거리 확인 (최소 픽셀 크기 5px, 최대 500px으로 완화)
                    double vDistance = Math.Abs(bottomLine.Coordinate - topLine.Coordinate);
                    if (vDistance < 5 || vDistance > 500) continue;

                    foreach (var leftLine in verticalLines.OrderBy(l => l.Coordinate)) // LineSegment의 Coordinate 사용
                    {
                        foreach (var rightLine in verticalLines.OrderBy(l => l.Coordinate)) // LineSegment의 Coordinate 사용
                        {
                            if (rightLine.Coordinate <= leftLine.Coordinate) continue; // 좌측 라인보다 오른쪽에 있어야 함

                            // 수평 거리 확인 (최소 픽셀 크기 5px, 최대 500px으로 완화)
                            double hDistance = Math.Abs(rightLine.Coordinate - leftLine.Coordinate);
                            if (hDistance < 5 || hDistance > 500) continue;

                            // 선들이 사각형을 형성하는지 확인 (허용 오차 범위 20px으로 확장)
                            // LineSegment의 Start, End 사용
                            if (IsRectangle(topLine.Coordinate, topLine.Start, topLine.End,
                                            bottomLine.Coordinate, bottomLine.Start, bottomLine.End,
                                            leftLine.Coordinate, leftLine.Start, leftLine.End,
                                            rightLine.Coordinate, rightLine.Start, rightLine.End,
                                            tolerance: 20))
                            {
                                var rect = new Rect(
                                    leftLine.Coordinate, // LineSegment의 Coordinate (X)
                                    topLine.Coordinate,  // LineSegment의 Coordinate (Y)
                                    hDistance,
                                    vDistance
                                );

                                // 중복 체크 (겹치는 비율이 높으면 중복으로 간주)
                                if (!IsOverlapping(rect, rectangles))
                                {
                                    rectangles.Add(rect);
                                }
                            }
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"감지된 초기 사각형: {rectangles.Count}개");
            return rectangles;
        }

        // 수평선 찾기
        private List<LineSegment> FindHorizontalLines(bool[,] edges, FloorPlanBounds bounds) // List<LineSegment> 반환
        {
            var lines = new List<LineSegment>(); // LineSegment 사용
            int minLineLength = 10; // 최소 선 길이 픽셀 (20에서 10으로 완화)

            for (int y = bounds.Top; y < bounds.Bottom; y += 1) // 1픽셀 간격으로 스캔
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
                        if (consecutivePixels >= minLineLength) // 최소 길이 이상의 선
                        {
                            lines.Add(new LineSegment(y, lineStart, x - 1)); // LineSegment 인스턴스 생성
                        }
                        lineStart = -1;
                        consecutivePixels = 0;
                    }
                }

                // 라인이 끝까지 이어지는 경우
                if (consecutivePixels >= minLineLength)
                {
                    lines.Add(new LineSegment(y, lineStart, bounds.Right - 1)); // LineSegment 인스턴스 생성
                }
            }

            // 가까운 선들 병합 (Y축 방향으로 가까운 선들)
            return MergeCloseLinesGeneric(lines, 10); // 직접 LineSegment 반환
        }

        // 수직선 찾기
        private List<LineSegment> FindVerticalLines(bool[,] edges, FloorPlanBounds bounds) // List<LineSegment> 반환
        {
            var lines = new List<LineSegment>(); // LineSegment 사용
            int minLineLength = 10; // 최소 선 길이 픽셀 (20에서 10으로 완화)

            for (int x = bounds.Left; x < bounds.Right; x += 1) // 1픽셀 간격으로 스캔
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
                        if (consecutivePixels >= minLineLength) // 최소 길이 이상의 선
                        {
                            lines.Add(new LineSegment(x, lineStart, y - 1)); // LineSegment 인스턴스 생성
                        }
                        lineStart = -1;
                        consecutivePixels = 0;
                    }
                }

                // 라인이 끝까지 이어지는 경우
                if (consecutivePixels >= minLineLength)
                {
                    lines.Add(new LineSegment(x, lineStart, bounds.Bottom - 1)); // LineSegment 인스턴스 생성
                }
            }

            // 가까운 선들 병합 (X축 방향으로 가까운 선들)
            return MergeCloseLinesGeneric(lines, 10); // 직접 LineSegment 반환
        }

        // 가까운 선들 병합 (Y, X축)
        private List<LineSegment> MergeCloseLinesGeneric( // LineSegment struct 사용
            List<LineSegment> lines, double mergeTolerance)
        {
            var merged = new List<LineSegment>();
            if (!lines.Any()) return merged;

            var sorted = lines.OrderBy(l => l.Coordinate).ToList();

            int i = 0;
            while (i < sorted.Count)
            {
                var current = sorted[i];
                double avgCoordinate = current.Coordinate;
                double minRange = current.Start;
                double maxRange = current.End;
                int count = 1;

                int j = i + 1;
                while (j < sorted.Count && Math.Abs(sorted[j].Coordinate - current.Coordinate) < mergeTolerance)
                {
                    // 범위가 겹치거나 매우 가까운 경우에만 병합
                    if (Math.Max(minRange, sorted[j].Start) < Math.Min(maxRange, sorted[j].End) + mergeTolerance) // + mergeTolerance로 겹침 허용 범위 확장
                    {
                        avgCoordinate = (avgCoordinate * count + sorted[j].Coordinate) / (count + 1);
                        minRange = Math.Min(minRange, sorted[j].Start);
                        maxRange = Math.Max(maxRange, sorted[j].End);
                        count++;
                    }
                    j++;
                }
                merged.Add(new LineSegment(avgCoordinate, minRange, maxRange)); // LineSegment 인스턴스 생성
                i = j; // 다음 병합 시작점
            }

            return merged;
        }

        // 이 메서드는 더 이상 FindHorizontalLines/FindVerticalLines에서 직접 사용되지 않습니다.
        // 대신 MergeCloseLinesGeneric을 직접 호출합니다.
        // 이 메서드를 삭제하거나, 현재 상태 유지
        /*
        private List<(double Y, double StartX, double EndX)> MergeCloseLines(
            List<(double Y, double StartX, double EndX)> lines, bool isHorizontal)
        {
            if (isHorizontal)
            {
                // LineSegment로 변환
                var genericLines = lines.Select(l => new LineSegment(l.Y, l.StartX, l.EndX)).ToList();
                var mergedGeneric = MergeCloseLinesGeneric(genericLines, 10); // 10픽셀 이내의 선들 병합
                // 다시 원래 튜플 형식으로 변환
                return mergedGeneric.Select(l => (Y: l.Coordinate, StartX: l.Start, EndX: l.End)).ToList();
            }
            else
            {
                // LineSegment로 변환
                var genericLines = lines.Select(l => new LineSegment(l.X, l.StartY, l.EndY)).ToList();
                var mergedGeneric = MergeCloseLinesGeneric(genericLines, 10); // 10픽셀 이내의 선들 병합
                // 다시 원래 튜플 형식으로 변환
                return mergedGeneric.Select(l => (X: l.Coordinate, StartY: l.Start, EndY: l.End)).ToList();
            }
        }
        */

        // 사각형 형성 확인
        // LineSegment의 멤버 이름에 맞춰 파라미터 변경
        private bool IsRectangle(
            double topLineCoordinate, double topLineStart, double topLineEnd,
            double bottomLineCoordinate, double bottomLineStart, double bottomLineEnd,
            double leftLineCoordinate, double leftLineStart, double leftLineEnd,
            double rightLineCoordinate, double rightLineStart, double rightLineEnd,
            double tolerance) // 허용 오차 파라미터 추가
        {
            // 각 꼭지점의 X, Y 좌표가 허용 오차 범위 내에 있는지 확인
            // topLine은 Y축 좌표, Start/End는 X 범위
            // leftLine은 X축 좌표, Start/End는 Y 범위
            bool topLeftOK = Math.Abs(leftLineCoordinate - topLineStart) <= tolerance && Math.Abs(topLineCoordinate - leftLineStart) <= tolerance;
            bool topRightOK = Math.Abs(rightLineCoordinate - topLineEnd) <= tolerance && Math.Abs(topLineCoordinate - rightLineStart) <= tolerance;
            bool bottomLeftOK = Math.Abs(leftLineCoordinate - bottomLineStart) <= tolerance && Math.Abs(bottomLineCoordinate - leftLineEnd) <= tolerance;
            bool bottomRightOK = Math.Abs(rightLineCoordinate - bottomLineEnd) <= tolerance && Math.Abs(bottomLineCoordinate - rightLineEnd) <= tolerance;

            // 또한, 선들이 서로를 충분히 덮고 있어야 합니다. (겹침 최소 길이)
            // 수평선의 X 범위가 수직선들의 X 범위 안에, 수직선의 Y 범위가 수평선들의 Y 범위 안에
            bool xOverlap = Math.Max(topLineStart, bottomLineStart) <= Math.Min(leftLineCoordinate, rightLineCoordinate) + tolerance &&
                            Math.Max(leftLineCoordinate, rightLineCoordinate) <= Math.Min(topLineEnd, bottomLineEnd) + tolerance;

            bool yOverlap = Math.Max(leftLineStart, rightLineStart) <= Math.Min(topLineCoordinate, bottomLineCoordinate) + tolerance &&
                            Math.Max(topLineCoordinate, bottomLineCoordinate) <= Math.Min(leftLineEnd, rightLineEnd) + tolerance;


            return topLeftOK && topRightOK && bottomLeftOK && bottomRightOK && xOverlap && yOverlap;
        }

        // 인접한 사각형들을 그룹화하여 하나의 객체로 만듭니다. (특히 선반처럼 보이는 객체)
        // 이 부분을 효율적으로 재구현하여 성능 저하를 방지합니다.
        private List<Rect> GroupAdjacentRectangles(List<Rect> rectangles)
        {
            var groupedRects = new List<Rect>();
            var visitedRects = new bool[rectangles.Count]; // 인덱스 기반 방문 기록

            // X축 기준으로 정렬하여 효율성을 높입니다.
            var sortedRectIndices = Enumerable.Range(0, rectangles.Count)
                                               .OrderBy(i => rectangles[i].X)
                                               .ThenBy(i => rectangles[i].Y)
                                               .ToList();

            double groupToleranceXY = 20; // 인접 허용 오차 (픽셀, 15에서 20으로 상향)
            double sizeSimilarityTolerance = 0.3; // 크기 유사성 (20%에서 30%로 완화)

            foreach (int i in sortedRectIndices)
            {
                if (visitedRects[i]) continue;

                var currentGroup = new List<Rect> { rectangles[i] };
                var queue = new Queue<int>();
                queue.Enqueue(i);
                visitedRects[i] = true;

                while (queue.Any())
                {
                    int currentRectIdx = queue.Dequeue();
                    Rect currentRect = rectangles[currentRectIdx];

                    // 현재 사각형과 인접한 다른 사각형들을 탐색
                    for (int j = 0; j < rectangles.Count; j++)
                    {
                        if (visitedRects[j]) continue;

                        Rect otherRect = rectangles[j];

                        // 인접성 검사 (두 사각형이 서로에게 닿아있거나 틈이 허용 오차 내에 있는지)
                        // Rect.IntersectsWith를 포함하여 겹치는 경우도 인접으로 처리
                        bool isAdjacent = (currentRect.IntersectsWith(otherRect) ||
                                          (Math.Abs(currentRect.Right - otherRect.Left) <= groupToleranceXY && // currentRight - otherLeft
                                           Math.Max(currentRect.Top, otherRect.Top) < Math.Min(currentRect.Bottom, otherRect.Bottom) + groupToleranceXY) || // Y축 겹침 또는 근접
                                          (Math.Abs(currentRect.Left - otherRect.Right) <= groupToleranceXY && // currentLeft - otherRight
                                           Math.Max(currentRect.Top, otherRect.Top) < Math.Min(currentRect.Bottom, otherRect.Bottom) + groupToleranceXY) ||
                                          (Math.Abs(currentRect.Bottom - otherRect.Top) <= groupToleranceXY && // currentBottom - otherTop
                                           Math.Max(currentRect.Left, otherRect.Left) < Math.Min(currentRect.Right, otherRect.Right) + groupToleranceXY) || // X축 겹침 또는 근접
                                          (Math.Abs(currentRect.Top - otherRect.Bottom) <= groupToleranceXY && // currentTop - otherBottom
                                           Math.Max(currentRect.Left, otherRect.Left) < Math.Min(currentRect.Right, otherRect.Right) + groupToleranceXY));

                        // 크기 유사성 검사 (선반처럼 비슷한 크기의 모듈이 반복될 때)
                        bool isSimilarSize = Math.Abs(currentRect.Width - otherRect.Width) / Math.Max(currentRect.Width, otherRect.Width) < sizeSimilarityTolerance &&
                                             Math.Abs(currentRect.Height - otherRect.Height) / Math.Max(currentRect.Height, otherRect.Height) < sizeSimilarityTolerance;

                        // 두 조건 중 하나라도 만족하면 그룹에 포함 (조정 필요)
                        // 도면의 선반들은 완전히 붙어있지 않고 틈이 있으므로 인접성 위주로 검사
                        // 너무 다른 크기의 객체는 그룹화하지 않도록 isSimilarSize를 포함
                        if (isAdjacent && isSimilarSize)
                        {
                            currentGroup.Add(otherRect);
                            queue.Enqueue(j);
                            visitedRects[j] = true;
                        }
                    }
                }

                // 그룹화된 사각형들을 하나의 큰 Rect로 합치기
                if (currentGroup.Any())
                {
                    double minX = currentGroup.Min(r => r.X);
                    double minY = currentGroup.Min(r => r.Y);
                    double maxX = currentGroup.Max(r => r.Right);
                    double maxY = currentGroup.Max(r => r.Bottom);

                    groupedRects.Add(new Rect(minX, minY, maxX - minX, maxY - minY));
                }
            }

            return groupedRects;
        }


        // 객체 타입 추측
        // GuessObjectType 메서드 개선
        private testpro.Models.DetectedObjectType GuessObjectType(Rect bounds) // 명확한 DetectedObjectType 사용
        {
            double ratio = bounds.Width / bounds.Height;
            double area = bounds.Width * bounds.Height;

            // 도면 픽셀과 실제 스케일 간의 명확한 정보가 없으므로,
            // 현재 도면에 맞춰서 픽셀 크기 임계값을 조정합니다.
            // 쿱스켓 도면2.jpg를 기준으로 대략적인 픽셀 크기를 추정하여 조정합니다.

            // 기둥 (작은 정사각형에 가까움, 30x30 ~ 50x50 픽셀 범위)
            if (Math.Abs(bounds.Width - bounds.Height) < 10 && bounds.Width > 30 && bounds.Width < 60)
            {
                return testpro.Models.DetectedObjectType.Pillar;
            }

            // 냉장고 (세로가 긴 직사각형, 대략 40x80 ~ 60x100 픽셀 범위)
            // 그룹화 로직으로 큰 선반/벽면냉장고가 될 수 있으므로, 이 조건은 좀 더 엄격하게
            if (ratio >= 0.4 && ratio <= 0.7 && bounds.Width > 30 && bounds.Height > 70 && area > 2000 && area < 6000)
            {
                return testpro.Models.DetectedObjectType.Refrigerator;
            }

            // 선반 (긴 직사각형, 가로로 긴 형태 또는 세로로 긴 형태, 너비 80~150, 길이 300~600 픽셀)
            // 작은 사각형들이 그룹화된 후의 큰 사각형에 해당
            // area 임계값을 더 높여서 작은 잡음 사각형을 걸러냄
            if (area > 8000) // 최소 면적 8000 픽셀 (이전 5000에서 상향 조정)
            {
                if (ratio > 2.5) // 가로로 매우 긴 형태 (예: 카운터, 긴 선반)
                {
                    if (bounds.Height < 100) // 높이가 낮으면 계산대나 진열대
                        return testpro.Models.DetectedObjectType.Checkout; // Checkout으로도 분류될 수 있음
                    return testpro.Models.DetectedObjectType.Shelf;
                }
                else if (ratio < 0.4) // 세로로 매우 긴 형태 (예: 벽면 냉장고, 긴 선반)
                {
                    // 예를 들어, 너비가 좁고 길이가 매우 길면 벽면 냉장고 (RefrigeratorWall)
                    if (bounds.Width < 60 && bounds.Height > 200) // 얇고 긴 형태
                    {
                        return testpro.Models.DetectedObjectType.RefrigeratorWall;
                    }
                    return testpro.Models.DetectedObjectType.Shelf;
                }
                else if (ratio >= 0.8 && ratio <= 1.2) // 정사각형에 가까운 큰 객체
                {
                    return testpro.Models.DetectedObjectType.DisplayRackDouble; // 양면 진열대
                }
            }

            // 평면 냉동고 (넓고 낮은 형태, 너비 80~150, 높이 30~50 픽셀)
            if (ratio > 1.5 && bounds.Height < 70 && area > 5000 && area < 10000) // Height 임계값 70으로 늘림
            {
                return testpro.Models.DetectedObjectType.FreezerChest;
            }

            // 진열대 (Shelf보다 좀 더 크고 다양)
            if (area > 4000 && area < 8000) // 중간 크기 객체, Shelf보다 작거나 비율이 덜 극단적
            {
                if (ratio > 1.0 && ratio < 2.0) // 폭이 길고 높이는 중간인 경우
                {
                    return testpro.Models.DetectedObjectType.DisplayStand;
                }
                else if (ratio < 1.0 && ratio > 0.5) // 세로로 좀 더 긴 경우 (일반 냉장고와 구분)
                {
                    return testpro.Models.DetectedObjectType.DisplayStand;
                }
            }


            // 나머지 기본값
            return testpro.Models.DetectedObjectType.Unknown; // 추론할 수 없는 경우 Unknown으로 설정
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

                    // 겹치는 영역이 새 사각형이나 기존 사각형의 일정 비율 이상이면 중복으로 간주
                    // 0.6 (60%) 이상 겹치면 중복으로 간주 (이전 0.5에서 상향 조정)
                    if (overlapArea / newArea > 0.6 || overlapArea / existingArea > 0.6)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}