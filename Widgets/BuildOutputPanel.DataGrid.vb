' Widgets/BuildOutputPanel.DataGrid.vb - CustomDrawDataGrid implementation for errors and warnings
' Created: 2025-01-02
Imports System
Imports System.Collections.Generic
Imports Gtk
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing CustomDrawDataGrid implementation for BuildOutputPanel
    ''' </summary>
    Partial Public Class BuildOutputPanel
        Inherits Box
        
        ' ===== New DataGrid Fields =====
        'Private pErrorsDataGrid As CustomDrawDataGrid
        'Private pWarningsDataGrid As CustomDrawDataGrid
        Private pUseDataGrid As Boolean = True  ' Feature flag to switch implementations
        
        ' ===== Column Indices =====
        Private Enum ErrorColumns
            eIcon = 0
            eFile = 1
            eLine = 2
            eColumn = 3
            eCode = 4
            eMessage = 5
        End Enum
        
        Private Enum WarningColumns
            eIcon = 0
            eFile = 1
            eLine = 2
            eColumn = 3
            eCode = 4
            eMessage = 5
        End Enum
        
        ' ===== Create DataGrid Tabs =====
        
        
        ''' <summary>
        ''' Creates the errors tab using self-contained CustomDrawDataGrid (no ScrolledWindow)
        ''' </summary>
        Private Sub CreateErrorsTab()
            Try
                ' Create the data grid directly (it has its own scrollbar)
                pErrorsDataGrid = New CustomDrawDataGrid()
                
                ' Configure columns
                ConfigureErrorColumns()
                
                ' Set grid properties
                pErrorsDataGrid.ShowGridLines = True
                pErrorsDataGrid.AlternateRowColors = True
                pErrorsDataGrid.AllowColumnResize = True
                pErrorsDataGrid.AllowSort = True
                pErrorsDataGrid.MultiSelectEnabled = False
                
                ' Handle events
                AddHandler pErrorsDataGrid.RowDoubleClicked, AddressOf OnErrorRowDoubleClicked
                AddHandler pErrorsDataGrid.SelectionChanged, AddressOf OnErrorSelectionChanged
                AddHandler pErrorsDataGrid.RenderIcon, AddressOf OnErrorGridRenderIcon
                
                ' Create label with markup support
                Dim lErrorLabel As New Label()
                lErrorLabel.UseMarkup = True
                lErrorLabel.Markup = "Errors (0)"  ' Start with normal color
                
                ' Add directly to notebook (no ScrolledWindow wrapper)
                pNotebook.AppendPage(pErrorsDataGrid, lErrorLabel)
                
            Catch ex As Exception
                Console.WriteLine($"CreateErrorsTab error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Creates the warnings tab using self-contained CustomDrawDataGrid (no ScrolledWindow)
        ''' </summary>
        Private Sub CreateWarningsTab()
            Try
                ' Create the data grid directly (it has its own scrollbar)
                pWarningsDataGrid = New CustomDrawDataGrid()
                
                ' Configure columns
                ConfigureWarningColumns()
                
                ' Set grid properties
                pWarningsDataGrid.ShowGridLines = True
                pWarningsDataGrid.AlternateRowColors = True
                pWarningsDataGrid.AllowColumnResize = True
                pWarningsDataGrid.AllowSort = True
                pWarningsDataGrid.MultiSelectEnabled = False
                
                ' Handle events
                AddHandler pWarningsDataGrid.RowDoubleClicked, AddressOf OnWarningRowDoubleClicked
                AddHandler pWarningsDataGrid.SelectionChanged, AddressOf OnWarningSelectionChanged
                AddHandler pWarningsDataGrid.RenderIcon, AddressOf OnWarningGridRenderIcon
                
                ' Create label with markup support
                Dim lWarningLabel As New Label()
                lWarningLabel.UseMarkup = True
                lWarningLabel.Markup = "Warnings (0)"  ' Start with normal color
                
                ' Add directly to notebook (no ScrolledWindow wrapper)
                pNotebook.AppendPage(pWarningsDataGrid, lWarningLabel)
                
            Catch ex As Exception
                Console.WriteLine($"CreateWarningsTab error: {ex.Message}")
            End Try
        End Sub

       
        ' ===== Data Population Methods =====
        
        ''' <summary>
        ''' Shows build result using CustomDrawDataGrid
        ''' </summary>
        Private Sub ShowBuildResultDataGrid(vResult As BuildResult, vProjectRoot As String)
            Try
                pProjectRoot = vProjectRoot
                pBuildResult = vResult
                
                ' Clear everything
                pBuildErrors.Clear()
                pBuildWarnings.Clear()
                
                If pErrorsDataGrid IsNot Nothing Then
                    pErrorsDataGrid.ClearRows()
                End If
                
                If pWarningsDataGrid IsNot Nothing Then
                    pWarningsDataGrid.ClearRows()
                End If
                
                ' Add errors to data grid
                If vResult.Errors IsNot Nothing AndAlso pErrorsDataGrid IsNot Nothing Then
                    Console.WriteLine($"Adding {vResult.Errors.Count} errors to data grid")
                    
                    for each lError As BuildError in vResult.Errors
                        ' Add to internal list
                        pBuildErrors.Add(lError)
                        
                        ' Create row for data grid
                        Dim lRow As New DataGridRow()
                        lRow.Tag = lError  ' Store the error object for later retrieval
                        
                        ' Add cells
                        lRow.Cells.Add(CreateErrorIconCell())
                        lRow.Cells.Add(CreateTextCell(System.IO.Path.GetFileName(lError.FilePath)))
                        lRow.Cells.Add(CreateNumberCell(lError.Line))
                        lRow.Cells.Add(CreateNumberCell(lError.Column))
                        lRow.Cells.Add(CreateTextCell(lError.ErrorCode))
                        lRow.Cells.Add(CreateTextCell(lError.Message))
                        
                        ' Add row to grid
                        pErrorsDataGrid.AddRow(lRow)
                    Next
                End If
                
                ' Add warnings to data grid
                If vResult.Warnings IsNot Nothing AndAlso pWarningsDataGrid IsNot Nothing Then
                    Console.WriteLine($"Adding {vResult.Warnings.Count} warnings to data grid")
                    
                    for each lWarning As BuildWarning in vResult.Warnings
                        ' Add to internal list
                        pBuildWarnings.Add(lWarning)
                        
                        ' Create row for data grid
                        Dim lRow As New DataGridRow()
                        lRow.Tag = lWarning  ' Store the warning object for later retrieval
                        
                        ' Add cells
                        lRow.Cells.Add(CreateWarningIconCell())
                        lRow.Cells.Add(CreateTextCell(System.IO.Path.GetFileName(lWarning.FilePath)))
                        lRow.Cells.Add(CreateNumberCell(lWarning.Line))
                        lRow.Cells.Add(CreateNumberCell(lWarning.Column))
                        lRow.Cells.Add(CreateTextCell(lWarning.WarningCode))
                        lRow.Cells.Add(CreateTextCell(lWarning.Message))
                        
                        ' Add row to grid
                        pWarningsDataGrid.AddRow(lRow)
                    Next
                End If
                
                ' Update tab labels with counts
                UpdateTabLabels()
                UpdateCopyButtonState()
                
                ' Auto-switch to errors tab if there are errors
                If vResult.HasErrors Then
                    pNotebook.CurrentPage = 1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ShowBuildResultDataGrid error: {ex.Message}")
                ' Fallback to TreeView implementation
                ShowBuildResult(vResult, vProjectRoot)
            End Try
        End Sub
        
        ' ===== Cell Creation Helpers =====
        
        ''' <summary>
        ''' Creates an error icon cell
        ''' </summary>
        Private Function CreateErrorIconCell() As DataGridCell
            Dim lCell As New DataGridCell()
            lCell.Value = "error"  ' Icon identifier
            lCell.ForegroundColor = "#FF0000"  ' Red for errors
            Return lCell
        End Function
        
        ''' <summary>
        ''' Creates a warning icon cell
        ''' </summary>
        Private Function CreateWarningIconCell() As DataGridCell
            Dim lCell As New DataGridCell()
            lCell.Value = "warning"  ' Icon identifier
            lCell.ForegroundColor = "#FFA500"  ' Orange for warnings
            Return lCell
        End Function
        
'         ''' <summary>
'         ''' Creates a text cell
'         ''' </summary>
'         Private Function CreateTextCell(vText As String) As DataGridCell
'             Dim lCell As New DataGridCell()
'             lCell.Value = If(vText, "")
'             Return lCell
'         End Function
'         
'         ''' <summary>
'         ''' Creates a number cell
'         ''' </summary>
'         Private Function CreateNumberCell(vNumber As Integer) As DataGridCell
'             Dim lCell As New DataGridCell()
'             lCell.Value = vNumber
'             Return lCell
'         End Function
        
        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handles double-click on error grid row
        ''' </summary>
        Private Sub OnErrorGridRowDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
            Try
                If vRow?.Tag IsNot Nothing Then
                    Dim lError As BuildError = TryCast(vRow.Tag, BuildError)
                    If lError IsNot Nothing Then
                        RaiseEvent ErrorSelected(lError.FilePath, lError.Line, lError.Column)
                        RaiseEvent ErrorDoubleClicked(lError)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"OnErrorGridRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles selection change in error grid
        ''' </summary>
        Private Sub OnErrorGridSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer)
            Try
                ' Optional: Handle selection change if needed
                Console.WriteLine($"Error grid selection changed: Row {vRowIndex}, Column {vColumnIndex}")
            Catch ex As Exception
                Console.WriteLine($"OnErrorGridSelectionChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles double-click on warning grid row
        ''' </summary>
        Private Sub OnWarningGridRowDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
            Try
                If vRow?.Tag IsNot Nothing Then
                    Dim lWarning As BuildWarning = TryCast(vRow.Tag, BuildWarning)
                    If lWarning IsNot Nothing Then
                        RaiseEvent WarningSelected(lWarning.FilePath, lWarning.Line, lWarning.Column)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"OnWarningGridRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles selection change in warning grid
        ''' </summary>
        Private Sub OnWarningGridSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer)
            Try
                ' Optional: Handle selection change if needed
                Console.WriteLine($"Warning grid selection changed: Row {vRowIndex}, Column {vColumnIndex}")
            Catch ex As Exception
                Console.WriteLine($"OnWarningGridSelectionChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods Override =====
        
        ''' <summary>
        ''' Override CreateUI to use DataGrid implementation
        ''' </summary>
        Private Sub CreateUIWithDataGrid()
            Try
                ' Create header bar with copy button
                Dim lHeaderBox As New Box(Orientation.Horizontal, 6)
                lHeaderBox.HeightRequest = 32
                lHeaderBox.MarginStart = 6
                lHeaderBox.MarginEnd = 6
                lHeaderBox.MarginTop = 4
                lHeaderBox.MarginBottom = 4
                
                ' Create title label
                Dim lTitle As New Label("Build output")
                lTitle.Halign = Align.Start
                lHeaderBox.PackStart(lTitle, True, True, 0)
                
                ' Create copy button
                pCopyButton = New Button()
                pCopyButton.Label = "Copy Errors"
                pCopyButton.TooltipText = "Copy all Errors and Warnings to clipboard"
                pCopyButton.Sensitive = False
                AddHandler pCopyButton.Clicked, AddressOf OnCopyButtonClicked
                lHeaderBox.PackStart(pCopyButton, False, False, 0)
                
                ' Create send to AI button
                pSendToAIButton = New Button()
                pSendToAIButton.Label = "Send to AI"
                pSendToAIButton.TooltipText = "Send Errors to AI assistant for help"
                pSendToAIButton.Sensitive = False
                AddHandler pSendToAIButton.Clicked, AddressOf OnSendToAIButtonClicked
                lHeaderBox.PackStart(pSendToAIButton, False, False, 0)
                
                Me.PackStart(lHeaderBox, False, False, 0)
                
                ' Create notebook
                pNotebook = New Notebook()
                Me.PackStart(pNotebook, True, True, 0)
                
                ' Create output tab (still uses TextView)
                CreateOutputTab()
                
                CreateErrorsTab()
                
                CreateWarningsTab()
                
            Catch ex As Exception
                Console.WriteLine($"CreateUIWithDataGrid error: {ex.Message}")
                ' Fallback to original implementation
                CreateUI()
            End Try
        End Sub
        
    End Class
End Namespace