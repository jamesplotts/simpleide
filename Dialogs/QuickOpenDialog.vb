' Dialogs/QuickOpenDialog.vb - Ctrl+P "quick open" file switcher, live-filtered by
' filename substring as the user types
Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets

Namespace Dialogs

    ''' <summary>
    ''' Lists every source file passed in, live-filtered by filename substring as the user
    ''' types. Enter or double-click opens the selected file; Up/Down move the selection
    ''' without leaving the filter entry; Escape cancels
    ''' </summary>
    Public Class QuickOpenDialog
        Inherits Dialog

        ' ===== Private Fields =====
        Private pThemeManager As ThemeManager
        Private pEntry As CustomDrawTextBox
        Private pGrid As CustomDrawDataGrid
        Private pAllFiles As List(Of String)
        Private pSelectedFile As String

        ' ===== Public Properties =====

        ''' <summary>
        ''' The file the user chose - only meaningful when Run() returns ResponseType.Ok
        ''' </summary>
        Public ReadOnly Property SelectedFile As String
            Get
                Return pSelectedFile
            End Get
        End Property

        ' ===== Constructor =====

        ''' <summary>
        ''' Creates a new quick-open dialog
        ''' </summary>
        ''' <param name="vParent">Owning window, for centering and modal ownership</param>
        ''' <param name="vFiles">Every candidate file's full path</param>
        ''' <param name="vThemeManager">Optional ThemeManager for CustomDraw widget theming</param>
        Public Sub New(vParent As Gtk.Window, vFiles As List(Of String), Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New("Go to File", vParent, DialogFlags.Modal)
            pThemeManager = vThemeManager
            pAllFiles = If(vFiles, New List(Of String)).OrderBy(Function(f) System.IO.Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList()

            Try
                SetDefaultSize(560, 380)
                SetPosition(WindowPosition.CenterOnParent)
                BorderWidth = 10

                BuildUI()
                PopulateGrid(pAllFiles)
                ShowAll()

                pEntry.GrabFocus()

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog constructor error: {ex.Message}")
            End Try
        End Sub

        Private Sub BuildUI()
            Try
                Dim lMainBox As New Box(Orientation.Vertical, 6)

                pEntry = New CustomDrawTextBox("Type to filter...")
                pEntry.ThemeManager = pThemeManager
                AddHandler pEntry.Changed, AddressOf OnFilterChanged
                AddHandler pEntry.Activated, AddressOf OnEntryActivated
                AddHandler pEntry.InnerEntry.KeyPressEvent, AddressOf OnEntryKeyPress
                lMainBox.PackStart(pEntry, False, False, 0)

                pGrid = CreateGrid()
                lMainBox.PackStart(pGrid, True, True, 0)
                ContentArea.PackStart(lMainBox, True, True, 0)

                AddHandler pGrid.RowDoubleClicked, AddressOf OnRowDoubleClicked

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog.BuildUI error: {ex.Message}")
            End Try
        End Sub

        Private Function CreateGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                Dim lFileColumn As New DataGridColumn() With {
                    .Name = "File",
                    .Title = "File",
                    .Width = 180,
                    .MinWidth = 80,
                    .Resizable = True,
                    .Sortable = False,
                    .DataType = DataGridColumnType.eText
                }
                lGrid.Columns.Add(lFileColumn)

                Dim lPathColumn As New DataGridColumn() With {
                    .Name = "Path",
                    .Title = "Path",
                    .Width = 320,
                    .MinWidth = 100,
                    .Resizable = True,
                    .Sortable = False,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True,
                    .AutoExpand = True
                }
                lGrid.Columns.Add(lPathColumn)

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = False
                lGrid.MultiSelectEnabled = False
                lGrid.HeightRequest = 300

                If pThemeManager IsNot Nothing Then lGrid.SetThemeManager(pThemeManager)

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog.CreateGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        ''' <summary>
        ''' Repopulates the grid's rows - each row's Tag is the exact full file path it was
        ''' built from, so selection never needs to re-resolve anything
        ''' </summary>
        Private Sub PopulateGrid(vFiles As List(Of String))
            Try
                pGrid.ClearRows()
                If vFiles Is Nothing Then Return

                for each lFile in vFiles
                    Dim lRow As New DataGridRow()
                    lRow.Tag = lFile
                    lRow.Cells.Add(New DataGridCell(System.IO.Path.GetFileName(lFile)))
                    lRow.Cells.Add(New DataGridCell(lFile))
                    pGrid.AddRow(lRow)
                Next

                If pGrid.Rows.Count > 0 Then pGrid.SelectRow(0)

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog.PopulateGrid error: {ex.Message}")
            End Try
        End Sub

        Private Function FilterFiles(vFilter As String) As List(Of String)
            If String.IsNullOrWhiteSpace(vFilter) Then Return pAllFiles
            Return pAllFiles.Where(Function(f) System.IO.Path.GetFileName(f).IndexOf(vFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
        End Function

        Private Sub MoveSelection(vDelta As Integer)
            Try
                If pGrid.Rows.Count = 0 Then Return

                Dim lSelected As List(Of DataGridRow) = pGrid.GetSelectedRows()
                Dim lCurrentIndex As Integer = If(lSelected.Count > 0, pGrid.Rows.IndexOf(lSelected(0)), -1)
                Dim lNewIndex As Integer = lCurrentIndex + vDelta

                If lNewIndex < 0 Then lNewIndex = 0
                If lNewIndex >= pGrid.Rows.Count Then lNewIndex = pGrid.Rows.Count - 1

                pGrid.SelectRow(lNewIndex)

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog.MoveSelection error: {ex.Message}")
            End Try
        End Sub

        Private Sub ConfirmSelection()
            Try
                Dim lSelected As List(Of DataGridRow) = pGrid.GetSelectedRows()
                If lSelected.Count = 0 Then Return

                pSelectedFile = TryCast(lSelected(0).Tag, String)
                If pSelectedFile IsNot Nothing Then Respond(ResponseType.Ok)

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog.ConfirmSelection error: {ex.Message}")
            End Try
        End Sub

        ' ===== Event Handlers =====

        Private Sub OnFilterChanged(vSender As Object, vArgs As EventArgs)
            PopulateGrid(FilterFiles(pEntry.Text))
        End Sub

        Private Sub OnEntryActivated(vSender As Object, vArgs As EventArgs)
            ConfirmSelection()
        End Sub

        Private Sub OnEntryKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Down
                        MoveSelection(1)
                        vArgs.RetVal = True
                    Case Gdk.Key.Up
                        MoveSelection(-1)
                        vArgs.RetVal = True
                    Case Gdk.Key.Escape
                        Respond(ResponseType.Cancel)
                        vArgs.RetVal = True
                End Select

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog.OnEntryKeyPress error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnRowDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
            Try
                pSelectedFile = TryCast(vRow?.Tag, String)
                If pSelectedFile IsNot Nothing Then Respond(ResponseType.Ok)

            Catch ex As Exception
                Console.WriteLine($"QuickOpenDialog.OnRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
