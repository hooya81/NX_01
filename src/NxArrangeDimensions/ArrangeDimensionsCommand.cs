using System;
using System.Collections.Generic;
using System.Linq;
using NXOpen;
using NXOpen.Annotations;
using NXOpen.UF;

namespace NxArrangeDimensions
{
    /// <summary>치수를 쌓는 축 (치수선에 수직인 방향)</summary>
    internal enum StackAxis
    {
        None,
        X, // 수직 치수: 좌/우로 쌓음
        Y, // 수평 치수: 위/아래로 쌓음
    }

    /// <summary>
    /// 기준치수 선택 → 정렬할 치수 선택 → 형태(직렬/병렬/혼합)·간격 입력 → 기준치수 기준으로 배치.
    /// 1차 버전 지원 범위: 수평(HorizontalDimension) / 수직(VerticalDimension) 치수.
    /// </summary>
    internal sealed class ArrangeDimensionsCommand
    {
        private const string Title = "Arrange Dimensions";

        private readonly Session _session;
        private readonly UI _ui;

        public ArrangeDimensionsCommand(Session session, UI ui)
        {
            _session = session;
            _ui = ui;
        }

        public void Run()
        {
            Part workPart = _session.Parts.Work;
            if (workPart == null)
            {
                Error("작업 파트가 없습니다. 도면 파트를 연 뒤 실행하세요.");
                return;
            }

            // 1) 기준치수 선택
            Dimension refDim = SelectReferenceDimension();
            if (refDim == null) return;

            StackAxis axis = GetStackAxis(refDim);
            if (axis == StackAxis.None)
            {
                Error("지원하지 않는 치수 유형입니다: " + refDim.GetType().Name +
                      "\n현재 버전은 수평/수직 치수만 지원합니다.");
                return;
            }

            refDim.Highlight();
            try
            {
                // 2) 정렬할 치수 선택 (기준치수와 같은 유형만 사용)
                List<Dimension> targets = SelectTargetDimensions(refDim, axis);
                if (targets == null || targets.Count == 0) return;

                // 3) 간격/순서 입력
                ArrangeSettings settings = SettingsForm.Ask(ArrangeSettings.Load(), targets.Count);
                if (settings == null) return;
                settings.Save();

                // 4) 배치 계산 및 적용
                Apply(refDim, targets, axis, settings);
            }
            finally
            {
                refDim.Unhighlight();
            }
        }

        // ---------------------------------------------------------------- 선택

        private static Selection.MaskTriple[] DimensionMask()
        {
            return new[] { new Selection.MaskTriple(UFConstants.UF_dimension_type, 0, 0) };
        }

        private Dimension SelectReferenceDimension()
        {
            while (true)
            {
                TaggedObject obj;
                Point3d cursor;
                Selection.Response resp = _ui.SelectionManager.SelectTaggedObject(
                    "기준치수를 선택하세요",
                    Title + " - 기준치수",
                    Selection.SelectionScope.WorkPart,
                    Selection.SelectionAction.ClearAndEnableSpecific,
                    false, false,
                    DimensionMask(),
                    out obj, out cursor);

                if (resp != Selection.Response.ObjectSelected &&
                    resp != Selection.Response.ObjectSelectedByName)
                    return null; // 취소/뒤로

                var dim = obj as Dimension;
                if (dim != null) return dim;

                Error("치수를 선택하세요.");
            }
        }

        private List<Dimension> SelectTargetDimensions(Dimension refDim, StackAxis axis)
        {
            TaggedObject[] objs;
            Selection.Response resp = _ui.SelectionManager.SelectTaggedObjects(
                "정렬할 치수를 선택하세요 (기준치수와 같은 방향)",
                Title + " - 정렬 대상",
                Selection.SelectionScope.WorkPart,
                Selection.SelectionAction.ClearAndEnableSpecific,
                false, false,
                DimensionMask(),
                out objs);

            if (resp != Selection.Response.Ok || objs == null || objs.Length == 0)
                return null;

            var targets = new List<Dimension>();
            int skipped = 0;
            foreach (var o in objs)
            {
                var d = o as Dimension;
                if (d == null || d.Tag == refDim.Tag) continue;

                if (GetStackAxis(d) == axis) targets.Add(d);
                else skipped++;
            }

            if (targets.Count == 0)
            {
                Error("기준치수와 같은 방향의 치수가 선택되지 않았습니다.");
                return null;
            }

            if (skipped > 0)
            {
                Info("방향이 다른 치수 " + skipped + "개는 제외됩니다.");
            }

            return targets;
        }

        internal static StackAxis GetStackAxis(Dimension dim)
        {
            if (dim is HorizontalDimension) return StackAxis.Y;
            if (dim is VerticalDimension) return StackAxis.X;
            return StackAxis.None;
        }

        // ---------------------------------------------------------------- 적용

        private void Apply(Dimension refDim, List<Dimension> targets, StackAxis axis, ArrangeSettings settings)
        {
            Point3d refOrigin = refDim.AnnotationOrigin;
            double refCoord = Coord(refOrigin, axis);

            var items = new List<LayoutItem>(targets.Count);
            for (int i = 0; i < targets.Count; i++)
            {
                Point3d o = targets[i].AnnotationOrigin;
                items.Add(new LayoutItem(i, Coord(o, axis) - refCoord, SafeSize(targets[i])));
            }

            IDictionary<int, double> newOffsets =
                DimensionLayout.Compute(items, settings.Mode, settings.Spacing, settings.Order, settings.RowTolerance);

            Session.UndoMarkId mark = _session.SetUndoMark(Session.MarkVisibility.Visible, Title);

            int moved = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                double newOffset;
                if (!newOffsets.TryGetValue(i, out newOffset)) continue;

                Dimension dim = targets[i];
                Point3d o = dim.AnnotationOrigin;
                Point3d n = WithCoord(o, axis, refCoord + newOffset);

                if (Math.Abs(Coord(n, axis) - Coord(o, axis)) < 1e-9) continue;

                // 치수선(텍스트) 위치만 쌓는 축 방향으로 이동. 다른 축 좌표는 유지.
                dim.AnnotationOrigin = n;
                moved++;
            }

            if (moved == 0)
            {
                _session.DeleteUndoMark(mark, null);
                Info("이미 정렬되어 있습니다.");
                return;
            }

            _session.UpdateManager.DoUpdate(mark);
        }

        private static double SafeSize(Dimension d)
        {
            try { return Math.Abs(d.ComputedSize); }
            catch (NXException) { return 0.0; }
        }

        private static double Coord(Point3d p, StackAxis axis)
        {
            return axis == StackAxis.X ? p.X : p.Y;
        }

        private static Point3d WithCoord(Point3d p, StackAxis axis, double value)
        {
            return axis == StackAxis.X
                ? new Point3d(value, p.Y, p.Z)
                : new Point3d(p.X, value, p.Z);
        }

        // ---------------------------------------------------------------- 메시지

        private void Error(string msg)
        {
            _ui.NXMessageBox.Show(Title, NXMessageBox.DialogType.Error, msg);
        }

        private void Info(string msg)
        {
            _ui.NXMessageBox.Show(Title, NXMessageBox.DialogType.Information, msg);
        }
    }
}
