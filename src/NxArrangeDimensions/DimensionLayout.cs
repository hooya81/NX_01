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

    /// <summary>배치 형태</summary>
    public enum ArrangeMode
    {
        /// <summary>직렬: 모든 치수를 기준치수와 같은 줄에 정렬</summary>
        Serial = 0,

        /// <summary>병렬: 치수마다 한 줄씩, 기준치수에서 간격만큼 쌓음</summary>
        Parallel = 1,

        /// <summary>혼합: 이미 같은 줄에 있는 치수(체인)는 묶어서 쌓음</summary>
        Mixed = 2,
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
        /// <item>직렬: 모든 치수의 오프셋을 0(기준치수 줄)으로.</item>
        /// <item>병렬: 현재 있는 쪽(+/-)을 유지한 채 치수마다 한 줄씩 spacing 간격으로 쌓음.
        ///     기준치수와 거의 같은 줄에 있던 치수는 치수가 더 많은 쪽으로 보냄.</item>
        /// <item>혼합: 기준치수 rowTolerance 이내는 기준 줄에 맞추고,
        ///     서로 rowTolerance 이내인 치수는 같은 줄(체인)로 묶어 쌓음.</item>
        /// </list>
        /// </summary>
        public static IDictionary<int, double> Compute(
            IEnumerable<LayoutItem> items, ArrangeMode mode, double spacing, ArrangeOrder order, double rowTolerance)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (spacing <= 0) throw new ArgumentOutOfRangeException(nameof(spacing), "간격은 0보다 커야 합니다.");
            if (rowTolerance < 0) throw new ArgumentOutOfRangeException(nameof(rowTolerance));

            var list = items.ToList();
            var result = new Dictionary<int, double>();

            if (mode == ArrangeMode.Serial)
            {
                foreach (var it in list) result[it.Id] = 0.0;
                return result;
            }

            List<LayoutItem> pos = list.Where(i => i.Offset > rowTolerance).ToList();
            List<LayoutItem> neg = list.Where(i => i.Offset < -rowTolerance).ToList();
            List<LayoutItem> near = list.Where(i => Math.Abs(i.Offset) <= rowTolerance).ToList();

            if (mode == ArrangeMode.Parallel)
            {
                // 기준 줄 위의 치수도 한 줄씩 차지해야 하므로 치수가 많은 쪽(동수면 +)에 합침
                if (neg.Count > pos.Count) neg.AddRange(near); else pos.AddRange(near);
                ArrangeSide(pos, +1, spacing, order, 0.0, result);
                ArrangeSide(neg, -1, spacing, order, 0.0, result);
            }
            else
            {
                foreach (var it in near) result[it.Id] = 0.0;
                ArrangeSide(pos, +1, spacing, order, rowTolerance, result);
                ArrangeSide(neg, -1, spacing, order, rowTolerance, result);
            }

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
                if (last != null && rowTolerance > 0 &&
                    Math.Abs(it.Offset) - Math.Abs(last[0].Offset) <= rowTolerance)
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
