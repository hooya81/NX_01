Imports System
Imports NXOpen
Imports NXOpen.UF


' NX 내부 실행 DLL 진입점 (파일 -> 실행 -> NX Open)
' Siemens 공식 VB 패턴 (UGOPEN\NXOpenExamples\VB\GS_Guide\NXOpenWinFormApp)과 동일 구조
Public Class NxMain

    Public Shared theSession As Session = Nothing
    Public Shared theUfSession As UFSession = Nothing

    ' NX가 DLL 로드 후 호출하는 진입점. 반드시 Public Shared Sub Main()
    Public Shared Sub Main()

        System.Diagnostics.Debugger.Launch()

        theSession = Session.GetSession()
        theUfSession = UFSession.GetUFSession()

        'setRemoveNote("note")

        Using f As New FormMain(theSession)
            ' NX 내부에서는 Application.Run 대신 모달 Dialog로 표시
            f.ShowDialog()
        End Using

    End Sub

    Private Shared Sub setRemoveNote(ByVal keyword As String)

        Dim prt As Part = theSession.Parts.Work

        Dim target As New List(Of NXObject)

        For Each aNote As NXOpen.Annotations.Note In prt.Notes

            If String.IsNullOrEmpty(keyword) Then
                target.Add(aNote)
            Else
                If NoteContains(aNote, keyword) Then target.Add(aNote)
            End If
        Next

        Dim mid As Session.UndoMarkId = theSession.SetUndoMark(Session.MarkVisibility.Visible, "del note")


        Try
            theSession.UpdateManager.AddObjectsToDeleteList(target.ToArray)
            Dim er As Integer = theSession.UpdateManager.DoUpdate(mid)
        Catch ex As Exception
            theSession.UndoToMark(mid, Nothing)
            theSession.DeleteUndoMark(mid, "del note")
        Finally
            setDisplayUpdate(prt)
        End Try

    End Sub

    ''' <summary>
    ''' 화면 업데이트
    ''' </summary>
    ''' <param name="prt"></param>
    Private Shared Sub setDisplayUpdate(ByVal prt As Part)

        Try
            prt.ModelingViews.WorkView.Regenerate()
        Catch ex As Exception
        End Try

        Try
            NxMain.theUfSession.Disp.RegenerateDisplay()
        Catch ex As Exception
        End Try

    End Sub

    Private Shared Function NoteContains(ByVal aNote As NXOpen.Annotations.Note, ByVal keyword As String) As Boolean
        Try
            Dim lines As String() = aNote.GetText()
            If lines Is Nothing Then Return False

            For Each line As String In lines
                If line IsNot Nothing AndAlso
                   line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    Return True
                End If
            Next
        Catch ex As NXException
            ' 텍스트를 읽을 수 없는 NOTE는 건너뜀
        End Try
        Return False
    End Function


End Class
