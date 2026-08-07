' Dialogs/DefinitionPickerDialog.vb - Picker shown when Go to Definition finds matches for
' the same symbol in more than one solution project
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets

Namespace Dialogs

    ''' <summary>
    ''' One go-to-definition candidate found in a specific solution project
    ''' </summary>
    Public Class DefinitionCandidate

        ''' <summary>
        ''' The display name of the project this candidate was found in
        ''' </summary>
        Public Property ProjectName As String

        ''' <summary>
        ''' The definition location found in that project
        ''' </summary>
        Public Property Definition As DefinitionInfo

    End Class

    ''' <summary>
    ''' Shown when Go to Definition finds a symbol defined in more than one solution project
    ''' and there's no way to pick a winner automatically - lets the user choose which
    ''' project's definition to navigate to rather than silently guessing
    ''' </summary>
    Public Class DefinitionPickerDialog
        Inherits Dialog

        ' ===== Private Fields =====
        Private pThemeManager As ThemeManager
        Private pGrid As CustomDrawDataGrid
        Private pSelected As DefinitionCandidate

        ' ===== Public Properties =====

        ''' <summary>
        ''' The candidate the user chose - only meaningful when Run() returns
        ''' ResponseType.Ok
        ''' </summary>
        Public ReadOnly Property SelectedCandidate As DefinitionCandidate
            Get
                Return pSelected
            End Get
        End Property

        ' ===== Constructor =====

        ''' <summary>
        ''' Creates a new definition picker dialog
        ''' </summary>
        ''' <param name="vParent">Owning window, for centering and modal ownership</param>
        ''' <param name="vSymbolName">The symbol name being resolved, for the dialog title</param>
        ''' <param name="vCandidates">One candidate per project that defines the symbol</param>
        ''' <param name="vThemeManager">Optional ThemeManager for CustomDraw widget theming</param>
        Public Sub New(vParent As Window, vSymbolName As String, vCandidates As List(Of DefinitionCandidate), Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New($"Multiple definitions found for '{vSymbolName}'", vParent, DialogFlags.Modal)
            pThemeManager = vThemeManager

            Try
                SetDefaultSize(560, 320)
                SetPosition(WindowPosition.CenterOnParent)
                BorderWidth = 10

                BuildUI(vCandidates)
                ShowAll()

            Catch ex As Exception
                Console.WriteLine($"DefinitionPickerDialog constructor error: {ex.Message}")
            End Try
        End Sub

        Private Sub BuildUI(vCandidates As List(Of DefinitionCandidate))
            Try
                Dim lMainBox As New Box(Orientation.Vertical, 10)

                pGrid = CreateGrid()
                lMainBox.PackStart(pGrid, True, True, 0)
                ContentArea.PackStart(lMainBox, True, True, 0)

                PopulateGrid(vCandidates)

                Dim lButtonBox As New Box(Orientation.Horizontal, 6)
                lButtonBox.Halign = Align.End
                lButtonBox.BorderWidth = 6

                Dim lGoButton As New CustomDrawButton("Go to Definition")
                lGoButton.ThemeManager = pThemeManager
                AddHandler lGoButton.Clicked, AddressOf OnGoToDefinition
                lButtonBox.PackStart(lGoButton, False, False, 0)

                Dim lCancelButton As New CustomDrawButton("Cancel")
                lCancelButton.ThemeManager = pThemeManager
                AddHandler lCancelButton.Clicked, Sub() Respond(ResponseType.Cancel)
                lButtonBox.PackStart(lCancelButton, False, False, 0)

                Dim lContentBox As Box = TryCast(ContentArea, Box)
                If lContentBox IsNot Nothing Then lContentBox.PackStart(lButtonBox, False, False, 0)

                AddHandler pGrid.RowDoubleClicked, AddressOf OnRowDoubleClicked

            Catch ex As Exception
                Console.WriteLine($"DefinitionPickerDialog.BuildUI error: {ex.Message}")
            End Try
        End Sub

        Private Function CreateGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                Dim lProjectColumn As New DataGridColumn() With {
                    .Name = "Project",
                    .Title = "Project",
                    .Width = 160,
                    .MinWidth = 80,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText
                }
                lGrid.Columns.Add(lProjectColumn)

                Dim lFileColumn As New DataGridColumn() With {
                    .Name = "File",
                    .Title = "File",
                    .Width = 220,
                    .MinWidth = 100,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True,
                    .AutoExpand = True
                }
                lGrid.Columns.Add(lFileColumn)

                Dim lLineColumn As New DataGridColumn() With {
                    .Name = "Line",
                    .Title = "Line",
                    .Width = 60,
                    .MinWidth = 40,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eNumber
                }
                lGrid.Columns.Add(lLineColumn)

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = True
                lGrid.MultiSelectEnabled = False
                lGrid.HeightRequest = 220

                If pThemeManager IsNot Nothing Then lGrid.SetThemeManager(pThemeManager)

            Catch ex As Exception
                Console.WriteLine($"DefinitionPickerDialog.CreateGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        ''' <summary>
        ''' Populates the grid - each row's Tag is the exact DefinitionCandidate it was built
        ''' from, so selection never needs to re-resolve anything
        ''' </summary>
        Private Sub PopulateGrid(vCandidates As List(Of DefinitionCandidate))
            Try
                pGrid.ClearRows()
                If vCandidates Is Nothing Then Return

                for each lCandidate in vCandidates
                    Dim lRow As New DataGridRow()
                    lRow.Tag = lCandidate
                    lRow.Cells.Add(New DataGridCell(lCandidate.ProjectName))
                    lRow.Cells.Add(New DataGridCell(System.IO.Path.GetFileName(lCandidate.Definition.FilePath)))
                    lRow.Cells.Add(New DataGridCell((lCandidate.Definition.Line + 1).ToString()))
                    pGrid.AddRow(lRow)
                Next

                If pGrid.Rows.Count > 0 Then pGrid.SelectRow(0)

            Catch ex As Exception
                Console.WriteLine($"DefinitionPickerDialog.PopulateGrid error: {ex.Message}")
            End Try
        End Sub

        Private Function GetSelected() As DefinitionCandidate
            Try
                Dim lRows As List(Of DataGridRow) = pGrid.GetSelectedRows()
                If lRows.Count = 0 Then Return Nothing
                Return TryCast(lRows(0).Tag, DefinitionCandidate)

            Catch ex As Exception
                Console.WriteLine($"DefinitionPickerDialog.GetSelected error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        Private Sub OnRowDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
            Try
                Dim lCandidate As DefinitionCandidate = TryCast(vRow?.Tag, DefinitionCandidate)
                If lCandidate Is Nothing Then Return

                pSelected = lCandidate
                Respond(ResponseType.Ok)

            Catch ex As Exception
                Console.WriteLine($"DefinitionPickerDialog.OnRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnGoToDefinition(vSender As Object, vArgs As EventArgs)
            Try
                Dim lCandidate As DefinitionCandidate = GetSelected()
                If lCandidate Is Nothing Then Return

                pSelected = lCandidate
                Respond(ResponseType.Ok)

            Catch ex As Exception
                Console.WriteLine($"DefinitionPickerDialog.OnGoToDefinition error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
