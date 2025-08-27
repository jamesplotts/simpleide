' FindReplacePanel.SortableResults.vb
' Enhanced results view with sortable columns
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
        
        ' Sorting state
        Private pCurrentSortColumn As Integer = -1
        Private pCurrentSortOrder As SortType = SortType.Ascending
        Private pTreeModelSort As TreeModelSort
        
        ' Column indices for the ListStore
        Private Enum ResultColumns
            FileName = 0
            LineText = 1
            LineNumber = 2
            ColumnNumber = 3
            MatchText = 4
            FilePath = 5  ' Hidden column with full path for sorting
        End Enum
        
        ''' <summary>
        ''' Creates a tree view for search results with sortable columns
        ''' </summary>
        ''' <returns>Configured TreeView for displaying search results</returns>
        Private Function CreateSortableResultsView() As TreeView
            Try
                ' Create the list store model
                pResultsStore = New ListStore(
                    GetType(String),  ' 0: FileName
                    GetType(String),  ' 1: LineText  
                    GetType(Integer), ' 2: LineNumber
                    GetType(Integer), ' 3: ColumnNumber
                    GetType(String),  ' 4: MatchText
                    GetType(String)   ' 5: FilePath (hidden)
                )
                
                ' Create sortable model wrapper
                pTreeModelSort = New TreeModelSort(pResultsStore)
                
                ' Create tree view with the sortable model
                Dim lTreeView As New TreeView(pTreeModelSort)
                
                ' FIXED: Enable single-click activation mode
                ' This makes single-click behave like activation
                lTreeView.ActivateOnSingleClick = True
                
                ' File column
                Dim lFileColumn As New TreeViewColumn()
                lFileColumn.Title = "File"
                lFileColumn.Resizable = True
                lFileColumn.SortColumnId = CInt(ResultColumns.FileName)
                
                Dim lFileRenderer As New CellRendererText()
                lFileColumn.PackStart(lFileRenderer, True)
                lFileColumn.AddAttribute(lFileRenderer, "text", CInt(ResultColumns.FileName))
                lTreeView.AppendColumn(lFileColumn)
                
                ' Line column
                Dim lLineColumn As New TreeViewColumn()
                lLineColumn.Title = "Line"
                lLineColumn.Resizable = True
                lLineColumn.SortColumnId = CInt(ResultColumns.LineNumber)
                
                Dim lLineRenderer As New CellRendererText()
                lLineColumn.PackStart(lLineRenderer, True)
                lLineColumn.AddAttribute(lLineRenderer, "text", CInt(ResultColumns.LineNumber))
                lTreeView.AppendColumn(lLineColumn)
                
                ' Column column
                Dim lColColumn As New TreeViewColumn()
                lColColumn.Title = "Col"
                lColColumn.Resizable = True
                lColColumn.SortColumnId = CInt(ResultColumns.ColumnNumber)
                
                Dim lColRenderer As New CellRendererText()
                lColColumn.PackStart(lColRenderer, True)
                lColColumn.AddAttribute(lColRenderer, "text", CInt(ResultColumns.ColumnNumber))
                lTreeView.AppendColumn(lColColumn)
                
                ' Line text column
                Dim lTextColumn As New TreeViewColumn()
                lTextColumn.Title = "Line Text"
                lTextColumn.Resizable = True
                lTextColumn.Expand = True
                lTextColumn.SortColumnId = CInt(ResultColumns.LineText)
                
                Dim lTextRenderer As New CellRendererText()
                lTextRenderer.Ellipsize = Pango.EllipsizeMode.End
                lTextColumn.PackStart(lTextRenderer, True)
                lTextColumn.AddAttribute(lTextRenderer, "text", CInt(ResultColumns.LineText))
                lTreeView.AppendColumn(lTextColumn)
                
                ' Configure tree view properties
                lTreeView.EnableSearch = True
                lTreeView.SearchColumn = CInt(ResultColumns.LineText)
                lTreeView.HeadersVisible = True
                lTreeView.HeadersClickable = True
                ' RulesHint is obsolete in GTK# 3, removed
                
                ' Set default sort by line number
                pTreeModelSort.SetSortColumnId(CInt(ResultColumns.LineNumber), SortType.Ascending)
                
                Return lTreeView
                
            Catch ex As Exception
                Console.WriteLine($"CreateSortableResultsView error: {ex.Message}")
                ' Return basic tree view as fallback
                Return New TreeView()
            End Try
        End Function
        
        ''' <summary>
        ''' Sets up custom sort functions for each column
        ''' </summary>
        Private Sub SetupSortFunctions()
            Try
                If pTreeModelSort Is Nothing Then Return
                
                ' File path sort (case-insensitive, natural sort for file paths)
                pTreeModelSort.SetSortFunc(CInt(ResultColumns.FilePath), 
                    Function(model As ITreeModel, a As TreeIter, b As TreeIter) As Integer
                        Try
                            Dim lPathA As String = CStr(model.GetValue(a, CInt(ResultColumns.FilePath)))
                            Dim lPathB As String = CStr(model.GetValue(b, CInt(ResultColumns.FilePath)))
                            Console.WriteLine($"FileSort here")
                            ' Handle nulls
                            If String.IsNullOrEmpty(lPathA) AndAlso String.IsNullOrEmpty(lPathB) Then Return 0
                            If String.IsNullOrEmpty(lPathA) Then Return -1
                            If String.IsNullOrEmpty(lPathB) Then Return 1
                            
                            ' First compare directory paths
                            Dim lDirA As String = System.IO.Path.GetDirectoryName(lPathA)
                            Dim lDirB As String = System.IO.Path.GetDirectoryName(lPathB)
                            
                            Dim lDirCompare As Integer = String.Compare(lDirA, lDirB, StringComparison.OrdinalIgnoreCase)
                            If lDirCompare <> 0 Then Return lDirCompare
                            
                            ' Then compare file names
                            Dim lFileA As String = System.IO.Path.GetFileName(lPathA)
                            Dim lFileB As String = System.IO.Path.GetFileName(lPathB)
                            
                            Return String.Compare(lFileA, lFileB, StringComparison.OrdinalIgnoreCase)
                            
                        Catch ex As Exception
                            Console.WriteLine($"File sort error: {ex.Message}")
                            Return 0
                        End Try
                    End Function
                )
                
                ' Line number sort (numeric)
                pTreeModelSort.SetSortFunc(CInt(ResultColumns.LineNumber),
                    Function(model As ITreeModel, a As TreeIter, b As TreeIter) As Integer
                        Try
                            Dim lLineA As Integer = CInt(model.GetValue(a, CInt(ResultColumns.LineNumber)))
                            Dim lLineB As Integer = CInt(model.GetValue(b, CInt(ResultColumns.LineNumber)))
                            Return lLineA.CompareTo(lLineB)
                        Catch ex As Exception
                            Console.WriteLine($"Line sort error: {ex.Message}")
                            Return 0
                        End Try
                    End Function
                )
                
                ' Column number sort (numeric)
                pTreeModelSort.SetSortFunc(CInt(ResultColumns.ColumnNumber),
                    Function(model As ITreeModel, a As TreeIter, b As TreeIter) As Integer
                        Try
                            Dim lColA As Integer = CInt(model.GetValue(a, CInt(ResultColumns.ColumnNumber)))
                            Dim lColB As Integer = CInt(model.GetValue(b, CInt(ResultColumns.ColumnNumber)))
                            Return lColA.CompareTo(lColB)
                        Catch ex As Exception
                            Console.WriteLine($"Column sort error: {ex.Message}")
                            Return 0
                        End Try
                    End Function
                )
                
                ' Text sort (case-insensitive)
                pTreeModelSort.SetSortFunc(CInt(ResultColumns.LineText),
                    Function(model As ITreeModel, a As TreeIter, b As TreeIter) As Integer
                        Try
                            Dim lTextA As String = CStr(model.GetValue(a, CInt(ResultColumns.LineText)))
                            Dim lTextB As String = CStr(model.GetValue(b, CInt(ResultColumns.LineText)))
                            
                            ' Handle nulls
                            If String.IsNullOrEmpty(lTextA) AndAlso String.IsNullOrEmpty(lTextB) Then Return 0
                            If String.IsNullOrEmpty(lTextA) Then Return -1
                            If String.IsNullOrEmpty(lTextB) Then Return 1
                            
                            Return String.Compare(lTextA, lTextB, StringComparison.OrdinalIgnoreCase)
                        Catch ex As Exception
                            Console.WriteLine($"Text sort error: {ex.Message}")
                            Return 0
                        End Try
                    End Function
                )
                
                Console.WriteLine("Sort functions set up successfully")
                
            Catch ex As Exception
                Console.WriteLine($"SetupSortFunctions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles column header clicks for sorting
        ''' </summary>
        Private Sub OnColumnHeaderClicked(vColumn As ResultColumns, vTreeColumn As TreeViewColumn)
            Try
                Dim lColumnId As Integer = CInt(vColumn)
                
                ' Toggle sort order if clicking the same column
                If pCurrentSortColumn = lColumnId Then
                    pCurrentSortOrder = If(pCurrentSortOrder = SortType.Ascending, 
                                          SortType.Descending, SortType.Ascending)
                Else
                    pCurrentSortColumn = lColumnId
                    pCurrentSortOrder = SortType.Ascending
                End If
                
                ' Apply the sort
                pTreeModelSort.SetSortColumnId(lColumnId, pCurrentSortOrder)
                
                ' Update column header to show sort indicator
                UpdateSortIndicators(vTreeColumn, pCurrentSortOrder)
                
                ' Update status
                Dim lColumnName As String = vColumn.ToString()
                Dim lOrder As String = If(pCurrentSortOrder = SortType.Ascending, "ascending", "descending")
                pStatusLabel.Text = $"Sorted by {lColumnName} ({lOrder})"
                
            Catch ex As Exception
                Console.WriteLine($"OnColumnHeaderClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates sort indicators on column headers
        ''' </summary>
        Private Sub UpdateSortIndicators(vActiveColumn As TreeViewColumn, vSortOrder As SortType)
            Try
                ' Clear all indicators first
                for each lColumn in pResultsView.Columns
                    lColumn.SortIndicator = False
                Next
                
                ' Set indicator on active column
                vActiveColumn.SortIndicator = True
                vActiveColumn.SortOrder = vSortOrder
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSortIndicators error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Enhanced method to populate results with full file path for sorting
        ''' </summary>
        Private Sub PopulateSortableResults(vResults As List(Of FindResult))
            Try
                ' Clear existing results
                pResultsStore.Clear()
                
                Console.WriteLine($"PopulateSortableResults: Adding {vResults.Count} results")
                
                ' Add each result with full path
                for each lResult in vResults
                    Dim lIter As TreeIter = pResultsStore.AppendValues(
                        System.IO.Path.GetFileName(lResult.FilePath),  ' 0: Display file name only
                        lResult.LineText,                              ' 1: Line text
                        lResult.LineNumber,                            ' 2: Line number
                        lResult.ColumnNumber,                          ' 3: Column number
                        lResult.MatchText,                             ' 4: Match text
                        lResult.FilePath                               ' 5: Full path for sorting
                    )
                Next
                
                Console.WriteLine($"PopulateSortableResults: Store now has {pResultsStore.IterNChildren()} items")
                
                ' Force a refresh of the view
                If pResultsView IsNot Nothing Then
                    pResultsView.QueueDraw()
                End If
                
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
        ''' Gets the currently selected result from the sorted view
        ''' </summary>
        Private Function GetSelectedResult() As FindResult
            Try
                Dim lSelection As TreeSelection = pResultsView.Selection
                Dim lIter As TreeIter = Nothing
                
                If lSelection.GetSelected(lIter) Then
                    ' Get values from the sorted model
                    Dim lFilePath As String = CStr(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.FilePath)))
                    Dim lLineNumber As Integer = CInt(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.LineNumber)))
                    Dim lColumnNumber As Integer = CInt(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.ColumnNumber)))
                    
                    ' Find the corresponding result
                    Return pSearchResults.FirstOrDefault(Function(r) 
                        Return r.FilePath = lFilePath AndAlso 
                               r.LineNumber = lLineNumber AndAlso 
                               r.ColumnNumber = lColumnNumber
                    End Function)
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetSelectedResult error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Exports results to CSV format
        ''' </summary>
        Public Function ExportResultsToCSV() As String
            Try
                Dim lCSV As New System.Text.StringBuilder()
                lCSV.AppendLine("File,Line,Column,Text,Match")
                
                ' Export in current sort order
                Dim lIter As TreeIter = Nothing
                If pTreeModelSort.GetIterFirst(lIter) Then
                    Do
                        Dim lFileName As String = CStr(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.FileName)))
                        Dim lLineNumber As Integer = CInt(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.LineNumber)))
                        Dim lColumnNumber As Integer = CInt(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.ColumnNumber)))
                        Dim lLineText As String = CStr(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.LineText)))
                        Dim lMatchText As String = CStr(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.MatchText)))
                        
                        ' Escape quotes in text
                        lLineText = lLineText.Replace("""", """""")
                        lMatchText = lMatchText.Replace("""", """""")
                        
                        lCSV.AppendLine($"""{lFileName}"",{lLineNumber},{lColumnNumber},""{lLineText}"",""{lMatchText}""")
                        
                    Loop While pTreeModelSort.IterNext(lIter)
                End If
                
                Return lCSV.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"ExportResultsToCSV error: {ex.Message}")
                Return String.Empty
            End Try
        End Function

        ''' <summary>
        ''' Creates a context menu for the results tree view
        ''' </summary>
        Private Function CreateResultsContextMenu() As Menu
            Try
                Dim lMenu As New Menu()
                
                ' Go to result
                Dim lGoToItem As New MenuItem("Go to Result")
                AddHandler lGoToItem.Activated, Sub(sender, e)
                    OnResultActivated(pResultsView, New RowActivatedArgs())
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
                    pResultsStore.Clear()
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
        ''' Handles right-click on results tree view
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
                    lClipboard.Text = $"{System.IO.Path.GetFileName(lResult.FilePath)}:{lResult.LineNumber}:{lResult.ColumnNumber}: {lResult.LineText}"
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
        ''' Connects the enhanced events including context menu
        ''' </summary>
        Private Sub ConnectSortableEvents()
            Try
                ' Connect existing events
                ConnectEvents()
                
                ' Add context menu handler for results view
                If pResultsView IsNot Nothing Then
                    AddHandler pResultsView.ButtonPressEvent, AddressOf OnResultsButtonPress
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ConnectSortableEvents error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles keyboard navigation in the results tree view
        ''' </summary>
        Private Sub OnResultsKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Dim lModifiers As ModifierType = vArgs.Event.State
                Dim lKeyString As String = KeyboardHelper.GetKeyString(vArgs.Event.KeyValue)

                Select Case vArgs.Event.Key
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        ' Enter - go to result
                        OnResultActivated(pResultsView, New RowActivatedArgs())
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.F3
                        ' F3 - next result in same file
                        NavigateToNextInFile()
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.F3
                        If (lModifiers And Gdk.ModifierType.ShiftMask) = Gdk.ModifierType.ShiftMask Then
                            ' Handle Ctrl combinations first
                            ' Shift+F3 - previous result in same file
                            NavigateToPreviousInFile()
                            vArgs.RetVal = True
                         End If
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
                            pResultsView.Selection.SelectAll()
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
        ''' Selects a specific result in the tree view
        ''' </summary>
        Private Sub SelectResult(vResult As FindResult)
            Try
                If vResult Is Nothing Then Return
                
                ' Find the result in the tree model
                Dim lIter As TreeIter = Nothing
                If pTreeModelSort.GetIterFirst(lIter) Then
                    Do
                        Dim lFilePath As String = CStr(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.FilePath)))
                        Dim lLineNumber As Integer = CInt(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.LineNumber)))
                        Dim lColumnNumber As Integer = CInt(pTreeModelSort.GetValue(lIter, CInt(ResultColumns.ColumnNumber)))
                        
                        If lFilePath = vResult.FilePath AndAlso 
                           lLineNumber = vResult.LineNumber AndAlso 
                           lColumnNumber = vResult.ColumnNumber Then
                            
                            ' Select this row
                            pResultsView.Selection.SelectIter(lIter)
                            
                            ' Scroll to make it visible
                            Dim lPath As TreePath = pTreeModelSort.GetPath(lIter)
                            pResultsView.ScrollToCell(lPath, Nothing, False, 0, 0)
                            
                            ' Trigger navigation to the result
                            RaiseEvent ResultSelected(vResult.FilePath, vResult.LineNumber, vResult.ColumnNumber)
                            
                            Exit Do
                        End If
                        
                    Loop While pTreeModelSort.IterNext(lIter)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectResult error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Highlights all results in the current file
        ''' </summary>
        Public Sub HighlightAllInCurrentFile()
            Try
                Dim lTab As TabInfo = GetCurrentTab()
                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then Return
                
                ' Get all results for current file
                Dim lFileResults = pSearchResults.Where(Function(r) r.FilePath = lTab.FilePath).ToList()
                
                If lFileResults.Count = 0 Then
                    pStatusLabel.Text = "No results in current file"
                    Return
                End If
                
                ' Convert to EditorPositions
                Dim lPositions As New List(Of EditorPosition)()
                For Each lResult In lFileResults
                    lPositions.Add(New EditorPosition(lResult.LineNumber - 1, lResult.ColumnNumber - 1))
                Next
                
                ' Highlight in editor (would need to implement this in the editor)
                ' For now, just update status
                pStatusLabel.Text = $"Found {lFileResults.Count} match(es) in current file"
                
            Catch ex As Exception
                Console.WriteLine($"HighlightAllInCurrentFile error: {ex.Message}")
            End Try
        End Sub


        ' Helper Methods
        Private Function GetCurrentSearchOptions() As SearchOptions
            Dim lOptions As SearchOptions
            lOptions.SearchText = pFindEntry.Text
            lOptions.ReplaceText = pReplaceEntry.Text
            lOptions.MatchCase = pCaseSensitiveCheck.Active
            lOptions.WholeWord = pWholeWordCheck.Active
            lOptions.UseRegex = pRegexCheck.Active
            lOptions.Scope = If(pInFileRadio.Active, SearchScope.eCurrentFile, SearchScope.eProject)
            lOptions.FileFilter = "*.vb" ' Default filter
            Return lOptions
        End Function

        
    End Class
    
End Namespace
