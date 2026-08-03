' FindReplacePanel.SortableResults.vb
' Results grid: construction, population, selection lookup, context menu, export,
' and F3/Shift+F3 in-file navigation
' Created: 2025-08-24

Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    Partial Public Class FindReplacePanel

        ''' <summary>
        ''' Creates the sortable, multi-column results grid (File / Line / Col / Line Text) -
        ''' a CustomDrawDataGrid rather than a Gtk.TreeView, reading real EditorTheme colors
        ''' directly instead of needing a GTK CSS override, and carrying each row's exact
        ''' source FindResult in DataGridRow.Tag so navigation never needs to re-search
        ''' </summary>
        Private Function CreateResultsGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                Dim lFileColumn As New DataGridColumn() With {
                    .Name = "File",
                    .Title = "File",
                    .Width = 140,
                    .MinWidth = 60,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True
                }
                lGrid.Columns.Add(lFileColumn)

                Dim lLineColumn As New DataGridColumn() With {
                    .Name = "Line",
                    .Title = "Line",
                    .Width = 60,
                    .MinWidth = 40,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eNumber,
                    .Alignment = ColumnAlignment.eRight
                }
                lGrid.Columns.Add(lLineColumn)

                Dim lColColumn As New DataGridColumn() With {
                    .Name = "Col",
                    .Title = "Col",
                    .Width = 50,
                    .MinWidth = 30,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eNumber,
                    .Alignment = ColumnAlignment.eRight
                }
                lGrid.Columns.Add(lColColumn)

                Dim lTextColumn As New DataGridColumn() With {
                    .Name = "LineText",
                    .Title = "Line Text",
                    .Width = 300,
                    .MinWidth = 100,
                    .Resizable = True,
                    .Sortable = True,
                    .DataType = DataGridColumnType.eText,
                    .Ellipsize = True,
                    .AutoExpand = True
                }
                lGrid.Columns.Add(lTextColumn)

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = True
                lGrid.MultiSelectEnabled = False

                AddHandler lGrid.ContentArea.ButtonPressEvent, AddressOf OnResultsButtonPress
                AddHandler lGrid.ContentArea.KeyPressEvent, AddressOf OnResultsKeyPress

            Catch ex As Exception
                Console.WriteLine($"CreateResultsGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        ''' <summary>
        ''' Populates the results grid from a fresh search - each row's Tag is the exact
        ''' FindResult it was built from, so selection/navigation never needs to re-search
        ''' </summary>
        Private Sub PopulateSortableResults(vResults As List(Of FindResult))
            Try
                pResultsGrid.ClearRows()

                for each lResult in vResults
                    Dim lRow As New DataGridRow()
                    lRow.Tag = lResult
                    lRow.Cells.Add(New DataGridCell(lResult.FileName, lResult.FileName))
                    lRow.Cells.Add(New DataGridCell(lResult.LineNumber, lResult.LineNumber.ToString()))
                    lRow.Cells.Add(New DataGridCell(lResult.ColumnNumber, lResult.ColumnNumber.ToString()))
                    lRow.Cells.Add(New DataGridCell(lResult.LineText, lResult.LineText))
                    pResultsGrid.AddRow(lRow)
                Next

            Catch ex As Exception
                Console.WriteLine($"PopulateSortableResults error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Groups results by file for better visualization
        ''' </summary>
        Private Function GroupResultsByFile(vResults As List(Of FindResult)) As Dictionary(Of String, List(Of FindResult))
            Try
                Return vResults.GroupBy(Function(r) r.FilePath) _
                              .ToDictionary(Function(g) g.Key, Function(g) g.ToList())

            Catch ex As Exception
                Console.WriteLine($"GroupResultsByFile error: {ex.Message}")
                Return New Dictionary(Of String, List(Of FindResult))()
            End Try
        End Function

        ''' <summary>
        ''' Gets the currently selected result, directly from the selected row's Tag
        ''' </summary>
        Private Function GetSelectedResult() As FindResult
            Try
                Dim lSelected As List(Of DataGridRow) = pResultsGrid.GetSelectedRows()
                If lSelected.Count = 0 Then Return Nothing
                Return TryCast(lSelected(0).Tag, FindResult)

            Catch ex As Exception
                Console.WriteLine($"GetSelectedResult error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Exports results to CSV format, in the grid's current visible (sorted) order
        ''' </summary>
        Public Function ExportResultsToCSV() As String
            Try
                Dim lCSV As New System.Text.StringBuilder()
                lCSV.AppendLine("File,Line,Column,Text,Match")

                for each lRow in pResultsGrid.GetVisibleRows()
                    Dim lResult As FindResult = TryCast(lRow.Tag, FindResult)
                    If lResult Is Nothing Then Continue for

                    Dim lLineText As String = If(lResult.LineText, "").Replace("""", """""")
                    Dim lMatchText As String = If(lResult.MatchText, "").Replace("""", """""")

                    lCSV.AppendLine($"""{lResult.FileName}"",{lResult.LineNumber},{lResult.ColumnNumber},""{lLineText}"",""{lMatchText}""")
                Next

                Return lCSV.ToString()

            Catch ex As Exception
                Console.WriteLine($"ExportResultsToCSV error: {ex.Message}")
                Return String.Empty
            End Try
        End Function

        ''' <summary>
        ''' Creates a context menu for the results grid
        ''' </summary>
        Private Function CreateResultsContextMenu() As Menu
            Try
                Dim lMenu As New Menu()

                ' Go to result
                Dim lGoToItem As New MenuItem("Go to Result")
                AddHandler lGoToItem.Activated, Sub(sender, e)
                    Dim lResult As FindResult = GetSelectedResult()
                    If lResult IsNot Nothing Then
                        RaiseEvent ResultSelected(lResult.FilePath, lResult.LineNumber, lResult.ColumnNumber)
                    End If
                End Sub
                lMenu.Add(lGoToItem)

                lMenu.Add(New SeparatorMenuItem())

                ' Copy file path
                Dim lCopyPathItem As New MenuItem("Copy File Path")
                AddHandler lCopyPathItem.Activated, AddressOf OnCopyFilePath
                lMenu.Add(lCopyPathItem)

                ' Copy result text
                Dim lCopyTextItem As New MenuItem("Copy Result Text")
                AddHandler lCopyTextItem.Activated, AddressOf OnCopyResultText
                lMenu.Add(lCopyTextItem)

                ' Copy all results
                Dim lCopyAllItem As New MenuItem("Copy All Results")
                AddHandler lCopyAllItem.Activated, AddressOf OnCopyAllResults
                lMenu.Add(lCopyAllItem)

                lMenu.Add(New SeparatorMenuItem())

                ' Export results
                Dim lExportItem As New MenuItem("Export Results to CSV...")
                AddHandler lExportItem.Activated, AddressOf OnExportResults
                lMenu.Add(lExportItem)

                lMenu.Add(New SeparatorMenuItem())

                ' Clear results
                Dim lClearItem As New MenuItem("Clear Results")
                AddHandler lClearItem.Activated, Sub()
                    pResultsGrid.ClearRows()
                    pSearchResults.Clear()
                    pStatusLabel.Text = "Results cleared"
                End Sub
                lMenu.Add(lClearItem)

                lMenu.ShowAll()
                Return lMenu

            Catch ex As Exception
                Console.WriteLine($"CreateResultsContextMenu error: {ex.Message}")
                Return New Menu()
            End Try
        End Function

        ''' <summary>
        ''' Handles right-click on the results grid
        ''' </summary>
        Private Sub OnResultsButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                If vArgs.Event.Button = 3 Then  ' Right mouse button
                    Dim lMenu As Menu = CreateResultsContextMenu()
                    lMenu.PopupAtPointer(vArgs.Event)
                    vArgs.RetVal = True  ' Mark as handled
                End If

            Catch ex As Exception
                Console.WriteLine($"OnResultsButtonPress error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Copies the file path of selected result to clipboard
        ''' </summary>
        Private Sub OnCopyFilePath(vSender As Object, vArgs As EventArgs)
            Try
                Dim lResult As FindResult = GetSelectedResult()
                If lResult IsNot Nothing Then
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                    lClipboard.Text = lResult.FilePath
                    pStatusLabel.Text = "File path copied to clipboard"
                End If

            Catch ex As Exception
                Console.WriteLine($"OnCopyFilePath error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Copies the result text to clipboard
        ''' </summary>
        Private Sub OnCopyResultText(vSender As Object, vArgs As EventArgs)
            Try
                Dim lResult As FindResult = GetSelectedResult()
                If lResult IsNot Nothing Then
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                    lClipboard.Text = $"{lResult.FileName}:{lResult.LineNumber}:{lResult.ColumnNumber}: {lResult.LineText}"
                    pStatusLabel.Text = "Result copied to clipboard"
                End If

            Catch ex As Exception
                Console.WriteLine($"OnCopyResultText error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Copies all results to clipboard
        ''' </summary>
        Private Sub OnCopyAllResults(vSender As Object, vArgs As EventArgs)
            Try
                Dim lText As New System.Text.StringBuilder()
                lText.AppendLine($"Search Results for '{pLastSearchOptions.SearchText}'")
                lText.AppendLine($"Found {pSearchResults.Count} match(es)")
                lText.AppendLine()

                ' Group by file for better readability
                Dim lGroupedResults = GroupResultsByFile(pSearchResults)

                For Each lFileGroup In lGroupedResults.OrderBy(Function(g) g.Key)
                    lText.AppendLine($"File: {lFileGroup.Key}")

                    For Each lResult In lFileGroup.Value.OrderBy(Function(r) r.LineNumber)
                        lText.AppendLine($"  Line {lResult.LineNumber}, Col {lResult.ColumnNumber}: {lResult.LineText}")
                    Next
                    lText.AppendLine()
                Next

                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                lClipboard.Text = lText.ToString()
                pStatusLabel.Text = $"Copied {pSearchResults.Count} results To clipboard"

            Catch ex As Exception
                Console.WriteLine($"OnCopyAllResults error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Exports results to a CSV file
        ''' </summary>
        Private Sub OnExportResults(vSender As Object, vArgs As EventArgs)
            Try
                ' Create file chooser dialog
                Using lDialog As New FileChooserDialog(
                    "Export Search Results",
                    CType(Toplevel, Gtk.Window),
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Export", ResponseType.Accept)

                    ' Set up filters
                    Dim lCsvFilter As New FileFilter()
                    lCsvFilter.Name = "CSV Files (*.csv)"
                    lCsvFilter.AddPattern("*.csv")
                    lDialog.AddFilter(lCsvFilter)

                    Dim lAllFilter As New FileFilter()
                    lAllFilter.Name = "All Files"
                    lAllFilter.AddPattern("*")
                    lDialog.AddFilter(lAllFilter)

                    ' Set default filename
                    lDialog.CurrentName = $"search_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv"

                    If lDialog.Run() = CInt(ResponseType.Accept) Then
                        Dim lFilePath As String = lDialog.Filename

                        ' Get CSV content
                        Dim lCSV As String = ExportResultsToCSV()

                        ' Write to file
                        System.IO.File.WriteAllText(lFilePath, lCSV)

                        pStatusLabel.Text = $"Results exported To {System.IO.Path.GetFileName(lFilePath)}"
                    End If
                End Using

            Catch ex As Exception
                Console.WriteLine($"OnExportResults error: {ex.Message}")
                pStatusLabel.Text = "Export failed: " & ex.Message
            End Try
        End Sub

        ''' <summary>
        ''' Handles keyboard navigation in the results grid - Up/Down/PageUp/PageDown/Home/
        ''' End are already handled internally by CustomDrawDataGrid; this adds the
        ''' find-panel-specific keys (Enter, F3/Shift+F3, Ctrl+C, Ctrl+A) on top
        ''' </summary>
        Private Sub OnResultsKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Dim lModifiers As ModifierType = vArgs.Event.State
                Dim lKeyString As String = KeyboardHelper.GetKeyString(vArgs.Event.KeyValue)

                Select Case vArgs.Event.Key
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        Dim lResult As FindResult = GetSelectedResult()
                        If lResult IsNot Nothing Then
                            RaiseEvent ResultSelected(lResult.FilePath, lResult.LineNumber, lResult.ColumnNumber)
                        End If
                        vArgs.RetVal = True

                    Case Gdk.Key.F3
                        ' F3 - next result in same file; Shift+F3 - previous result in same file
                        If (lModifiers And Gdk.ModifierType.ShiftMask) = Gdk.ModifierType.ShiftMask Then
                            NavigateToPreviousInFile()
                        Else
                            NavigateToNextInFile()
                        End If
                        vArgs.RetVal = True
                End Select
                ' Remember the compiler is case-insenstive so cannot tell between Gdk.Key.C or Gdk.Key.c
                Select Case lKeyString.ToLower().Trim()
                    Case "c"
                        If (lModifiers And Gdk.ModifierType.ControlMask) = Gdk.ModifierType.ControlMask Then
                            ' Handle Ctrl combinations first
                            ' Ctrl+C - copy result
                            OnCopyResultText(Nothing, Nothing)
                            vArgs.RetVal = True
                        End If
                    Case "a"
                        If (lModifiers And Gdk.ModifierType.ControlMask) = Gdk.ModifierType.ControlMask Then
                            ' Handle Ctrl combinations first
                            ' Ctrl+A - select all results
                            pResultsGrid.SelectAll()
                            vArgs.RetVal = True
                        End If
                End Select

            Catch ex As Exception
                Console.WriteLine($"OnResultsKeyPress error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Navigates to the next result in the same file
        ''' </summary>
        Private Sub NavigateToNextInFile()
            Try
                Dim lCurrentResult As FindResult = GetSelectedResult()
                If lCurrentResult Is Nothing Then Return

                ' Find next result in same file
                Dim lSameFileResults = pSearchResults.Where(Function(r) r.FilePath = lCurrentResult.FilePath) _
                                                     .OrderBy(Function(r) r.LineNumber) _
                                                     .ThenBy(Function(r) r.ColumnNumber) _
                                                     .ToList()

                Dim lCurrentIndex As Integer = lSameFileResults.IndexOf(lCurrentResult)
                If lCurrentIndex >= 0 AndAlso lCurrentIndex < lSameFileResults.Count - 1 Then
                    SelectResult(lSameFileResults(lCurrentIndex + 1))
                End If

            Catch ex As Exception
                Console.WriteLine($"NavigateToNextInFile error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Navigates to the previous result in the same file
        ''' </summary>
        Private Sub NavigateToPreviousInFile()
            Try
                Dim lCurrentResult As FindResult = GetSelectedResult()
                If lCurrentResult Is Nothing Then Return

                ' Find previous result in same file
                Dim lSameFileResults = pSearchResults.Where(Function(r) r.FilePath = lCurrentResult.FilePath) _
                                                     .OrderBy(Function(r) r.LineNumber) _
                                                     .ThenBy(Function(r) r.ColumnNumber) _
                                                     .ToList()

                Dim lCurrentIndex As Integer = lSameFileResults.IndexOf(lCurrentResult)
                If lCurrentIndex > 0 Then
                    SelectResult(lSameFileResults(lCurrentIndex - 1))
                End If

            Catch ex As Exception
                Console.WriteLine($"NavigateToPreviousInFile error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Selects a specific result in the results grid by matching it against each row's
        ''' Tag - SelectRow raises SelectionChanged, which OnResultsSelectionChanged already
        ''' turns into a ResultSelected event, so no separate RaiseEvent is needed here
        ''' </summary>
        Private Sub SelectResult(vResult As FindResult)
            Try
                If vResult Is Nothing Then Return

                Dim lRows As List(Of DataGridRow) = pResultsGrid.Rows
                for i As Integer = 0 To lRows.Count - 1
                    Dim lRowResult As FindResult = TryCast(lRows(i).Tag, FindResult)
                    If lRowResult IsNot Nothing AndAlso lRowResult.Equals(vResult) Then
                        pResultsGrid.SelectRow(i)
                        Exit for
                    End If
                Next

            Catch ex As Exception
                Console.WriteLine($"SelectResult error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
