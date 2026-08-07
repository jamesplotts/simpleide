' Widgets/SolutionSettingsTab.vb - Read-only view of a loaded solution's projects in
' build/dependency order, opened as a notebook tab (not a dialog)
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' Shows every project in a loaded solution, in the order they'll actually build
    ''' (SolutionManager's own topological sort over each project's real
    ''' &lt;ProjectReference&gt; entries), along with which sibling projects each one depends
    ''' on. Read-only - build order isn't manually editable since it's already derived
    ''' entirely from real project references, matching what dotnet build itself uses
    ''' </summary>
    ''' <remarks>
    ''' Deliberately does NOT implement IEditor, following the same pattern as
    ''' PreferencesTab/AssemblySettingsEditor - this is a settings view, not a text editor,
    ''' so none of IEditor's undo/redo/selection/syntax-tree surface applies. Opened via
    ''' MainWindow.OpenSolutionSettingsTab with TabInfo.Editor = Nothing and
    ''' IsSpecialTab = True, the established convention for this class of tab
    ''' </remarks>
    Public Class SolutionSettingsTab
        Inherits Box

        ' ===== Private Fields =====
        Private pSolutionManager As SolutionManager
        Private pThemeManager As ThemeManager
        Private pGrid As CustomDrawDataGrid

        ' ===== Constructor =====

        ''' <summary>
        ''' Creates a new Solution Settings tab
        ''' </summary>
        ''' <param name="vSolutionManager">The loaded solution to display</param>
        ''' <param name="vThemeManager">Optional ThemeManager for CustomDraw widget theming</param>
        Public Sub New(vSolutionManager As SolutionManager, Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New(Orientation.Vertical, 8)
            pSolutionManager = vSolutionManager
            pThemeManager = vThemeManager

            Try
                BorderWidth = 10
                BuildUI()
                PopulateGrid()

            Catch ex As Exception
                Console.WriteLine($"SolutionSettingsTab constructor error: {ex.Message}")
            End Try
        End Sub

        Private Sub BuildUI()
            Try
                Dim lSolutionName As String = System.IO.Path.GetFileNameWithoutExtension(
                    pSolutionManager?.CurrentSolution?.SolutionPath)
                Dim lProjectCount As Integer = If(pSolutionManager?.AllProjects.Count, 0)

                Dim lTitleLabel As New Label()
                lTitleLabel.Markup = $"<b>Solution: {GLib.Markup.EscapeText(lSolutionName)}</b>"
                lTitleLabel.Halign = Align.Start
                PackStart(lTitleLabel, False, False, 0)

                Dim lSubtitleLabel As New Label(
                    $"{lProjectCount} project(s), shown in the order they'll actually build " &
                    "(computed from each project's own ProjectReference entries)")
                lSubtitleLabel.Halign = Align.Start
                PackStart(lSubtitleLabel, False, False, 0)

                pGrid = CreateGrid()
                PackStart(pGrid, True, True, 0)

            Catch ex As Exception
                Console.WriteLine($"SolutionSettingsTab.BuildUI error: {ex.Message}")
            End Try
        End Sub

        Private Function CreateGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                Dim lOrderColumn As New DataGridColumn() With {
                    .Name = "Order",
                    .Title = "#",
                    .Width = 40,
                    .MinWidth = 30,
                    .Resizable = True,
                    .Sortable = False,
                    .DataType = DataGridColumnType.eNumber
                }
                lGrid.Columns.Add(lOrderColumn)

                Dim lProjectColumn As New DataGridColumn() With {
                    .Name = "Project",
                    .Title = "Project",
                    .Width = 220,
                    .MinWidth = 100,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText
                }
                lGrid.Columns.Add(lProjectColumn)

                Dim lDependsOnColumn As New DataGridColumn() With {
                    .Name = "DependsOn",
                    .Title = "Depends On",
                    .Width = 260,
                    .MinWidth = 100,
                    .Resizable = True,
                    .Sortable = False,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True,
                    .AutoExpand = True
                }
                lGrid.Columns.Add(lDependsOnColumn)

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = True
                lGrid.MultiSelectEnabled = False

                If pThemeManager IsNot Nothing Then lGrid.SetThemeManager(pThemeManager)

            Catch ex As Exception
                Console.WriteLine($"SolutionSettingsTab.CreateGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        ''' <summary>
        ''' Populates the grid - AllProjects is already returned in build/dependency order
        ''' by SolutionManager, so no re-sorting is needed here
        ''' </summary>
        Private Sub PopulateGrid()
            Try
                pGrid.ClearRows()
                If pSolutionManager Is Nothing Then Return

                Dim lProjects As IReadOnlyList(Of ProjectManager) = pSolutionManager.AllProjects
                for i As Integer = 0 To lProjects.Count - 1
                    Dim lProject As ProjectManager = lProjects(i)

                    Dim lRow As New DataGridRow()
                    lRow.Tag = lProject
                    lRow.Cells.Add(New DataGridCell(i + 1))
                    lRow.Cells.Add(New DataGridCell(lProject.CurrentProjectName))
                    lRow.Cells.Add(New DataGridCell(BuildDependsOnText(lProject)))
                    pGrid.AddRow(lRow)
                Next

            Catch ex As Exception
                Console.WriteLine($"SolutionSettingsTab.PopulateGrid error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds a "depends on" display string from a project's own real
        ''' &lt;ProjectReference&gt; entries - the exact same reference set
        ''' SolutionManager.BuildDependencyOrder used to compute the build order shown here
        ''' </summary>
        Private Function BuildDependsOnText(vProject As ProjectManager) As String
            Try
                Dim lNames As New List(Of String)
                for each lRef in vProject.ProjectReferences
                    If lRef.Type <> ReferenceManager.ReferenceType.eProject Then Continue for
                    lNames.Add(lRef.Name)
                Next

                If lNames.Count = 0 Then Return "(none)"
                Return String.Join(", ", lNames)

            Catch ex As Exception
                Console.WriteLine($"SolutionSettingsTab.BuildDependsOnText error: {ex.Message}")
                Return "(unknown)"
            End Try
        End Function

    End Class

End Namespace
