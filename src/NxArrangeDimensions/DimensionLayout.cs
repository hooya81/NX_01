using System;
using System.Collections.Generic;
using System.Linq;

namespace NxArrangeDimensions
{
    /// <summary>정렬 순서</summary>
    public enum ArrangeOrder
    {
        /// <summary>현재 위치 순서 유지 (기준치수에 가까운 것이 안쪽)</summary>
        KeepCurrent = 0,

        /// <summary>치수값이 작은 것이 안쪽, 큰 것이 바깥쪽</summary>
        BySizeAscending = 1,
    }

    /// <summary>배치 계산 입력 1건 (NX 객체와 무관한 순수 데이터)</summary>
    public sealed class LayoutItem
    {
        public LayoutItem(int id, double offset, double size)
        {
            Id = id;
            Offset = offset;
            Size = size;
        }

        /// <summary>호출측 식별자 (목록 인덱스 등)</summary>
        public int Id { get; }

        /// <summary>기준치수 원점으로부터의 현재 거리 (쌓는 축 방향, 부호 포함)</summary>
        public double Offset { get; }

        /// <summary>치수값 (정렬 기준)</summary>
        public double Size { get; }
    }

    /// <summary>
    /// 기준치수로부터 일정 간격으로 치수를 쌓는 배치 계산.
    /// NX API를 쓰지 않으므로 단독 테스트 가능.
    /// </summary>
    public static class DimensionLayout
    {
        /// <summary>
        /// 각 치수의 새 오프셋(기준치수 원점 기준)을 계산한다.
        /// <list type="bullet">
        /// <item>기준치수와 rowTolerance 이내에 있는 치수는 기준치수 줄(오프셋 0)에 맞춘다.</item>
        /// <item>나머지는 현재 있는 쪽(+/-)을 유지한 채 spacing 간격으로 쌓는다.</item>
        /// <item>서로 rowTolerance 이내에 있는 치수는 같은 줄(체인 치수)로 보고 함께 이동한다.</item>
        /// </list>
        /// </summary>
        public static IDictionary<int, double> Compute(
            IEnumerable<LayoutItem> items, double spacing, ArrangeOrder order, double rowTolerance)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (spacing <= 0) throw new ArgumentOutOfRangeException(nameof(spacing), "간격은 0보다 커야 합니다.");
            if (rowTolerance < 0) throw new ArgumentOutOfRangeException(nameof(rowTolerance));

            var list = items.ToList();
            var result = new Dictionary<int, double>();

            foreach (var it in list.Where(i => Math.Abs(i.Offset) <= rowTolerance))
                result[it.Id] = 0.0;

            ArrangeSide(list.Where(i => i.Offset > rowTolerance), +1, spacing, order, rowTolerance, result);
            ArrangeSide(list.Where(i => i.Offset < -rowTolerance), -1, spacing, order, rowTolerance, result);

            return result;
        }

        private static void ArrangeSide(
            IEnumerable<LayoutItem> side, int sign, double spacing, ArrangeOrder order,
            double rowTolerance, IDictionary<int, double> result)
        {
            // 1) 현재 거리순으로 정렬 후, 가까이 붙어있는 것끼리 한 줄로 묶음
            var rows = new List<List<LayoutItem>>();
            foreach (var it in side.OrderBy(i => Math.Abs(i.Offset)))
            {
                var last = rows.Count > 0 ? rows[rows.Count - 1] : null;
                if (last != null && Math.Abs(it.Offset) - Math.Abs(last[0].Offset) <= rowTolerance)
                    last.Add(it);
                else
                    rows.Add(new List<LayoutItem> { it });
            }

            // 2) 줄 순서 결정
            if (order == ArrangeOrder.BySizeAscending)
            {
                rows = rows
                    .OrderBy(r => r.Max(i => Math.Abs(i.Size)))
                    .ThenBy(r => Math.Abs(r[0].Offset))
                    .ToList();
            }

            // 3) 간격 적용
            for (int k = 0; k < rows.Count; k++)
            {
                double newOffset = sign * (k + 1) * spacing;
                foreach (var it in rows[k])
                    result[it.Id] = newOffset;
            }
        }
    }
}
