using System;
using System.Globalization;
using System.IO;

namespace NxArrangeDimensions
{
    /// <summary>사용자 입력값. 마지막 값을 %APPDATA%에 저장해 다음 실행 때 기본값으로 사용.</summary>
    public sealed class ArrangeSettings
    {
        public double Spacing { get; set; } = 10.0;
        public ArrangeOrder Order { get; set; } = ArrangeOrder.KeepCurrent;

        /// <summary>같은 줄로 간주하는 허용 거리 = Spacing × 이 비율</summary>
        public const double RowToleranceRatio = 0.25;

        public double RowTolerance => Spacing * RowToleranceRatio;

        private static string FilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NxArrangeDimensions", "settings.txt");

        public static ArrangeSettings Load()
        {
            var s = new ArrangeSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split(new[] { '=' }, 2);
                    if (p.Length != 2) continue;
                    var key = p[0].Trim();
                    var val = p[1].Trim();

                    double d;
                    int n;
                    if (key == "Spacing" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d > 0)
                        s.Spacing = d;
                    else if (key == "Order" && int.TryParse(val, out n) && Enum.IsDefined(typeof(ArrangeOrder), n))
                        s.Order = (ArrangeOrder)n;
                }
            }
            catch
            {
                // 설정 파일 문제는 무시하고 기본값 사용
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new[]
                {
                    "Spacing=" + Spacing.ToString(CultureInfo.InvariantCulture),
                    "Order=" + ((int)Order).ToString(CultureInfo.InvariantCulture),
                });
            }
            catch
            {
                // 저장 실패는 기능에 영향 없음
            }
        }
    }
}
