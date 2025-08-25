' MainWindow.BottomPanel.vb - Bottom panel management for MainWindow (FIXED)
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Partial Public Class MainWindow
    
    ' ===== Bottom Panel Management =====

    Private ReadOnly Property pErrorListView() As TreeView
        Get
            Return pBottomPanelManager.ErrorListView
        End Get
    End Property
    
    ' Show bottom panel with specific tab
    Public Sub ShowBottomPanel(Optional vTabIndex As Integer = -1)
        Try
            pBottomPanelManager.Show()
            pBottomPanelManager.ShowTab(vTabIndex)
            pBottomPanelVisible = True
            
            ' Adjust paned position if needed
            If pCenterVPaned IsNot Nothing Then
                Dim lHeight As Integer = pSettingsManager.BottomPanelHeight
                If lHeight < 50 Then lHeight = BOTTOM_PANEL_HEIGHT
                
                If pCenterVPaned.AllocatedHeight > 0 Then
                    Dim lMaxPosition As Integer = pCenterVPaned.AllocatedHeight - 50
                    pCenterVPaned.Position = Math.Max(50, Math.Min(lMaxPosition, pCenterVPaned.AllocatedHeight - lHeight))
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ShowBottomPanel error: {ex.Message}")
        End Try
    End Sub
    
    ' Hide bottom panel
    Public Sub HideBottomPanel()
        Try
            pBottomPanelManager.HidePanel()
            pBottomPanelManager.Hide()
            pBottomPanelVisible = False
            
            ' Return focus to editor if available
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing AndAlso lEditor.Widget IsNot Nothing Then
                lEditor.Widget.GrabFocus()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"HideBottomPanel error: {ex.Message}")
        End Try
    End Sub
    
    ' Toggle bottom panel visibility
    Public Sub ToggleBottomPanel()
        If pBottomPanelVisible Then
            HideBottomPanel()
        Else
            ShowBottomPanel()
        End If
    End Sub
    
    ' Initialize bottom panel code integrated into BottomPanelManager Class in BottomPanelManager.vb
    Private Sub InitializeBottomPanel()
        Try
            ' Nothing here
        Catch ex As Exception
            Console.WriteLine($"InitializeBottomPanel error: {ex.Message}")
        End Try
    End Sub
    
    ' Create error list view
    Private Function CreateErrorListView() As TreeView
        Try
            ' Create list store: Type, Code, Description, File, Line, Column
            Dim lStore As New ListStore(GetType(String), GetType(String), GetType(String), 
                                      GetType(String), GetType(Integer), GetType(Integer))
            
            Dim lTreeView As New TreeView(lStore)
            
            ' Add columns
            ' Icon column
            Dim lIconRenderer As New CellRendererPixbuf()
            Dim lIconColumn As New TreeViewColumn("", lIconRenderer)
            lIconColumn.SetCellDataFunc(lIconRenderer, AddressOf RenderErrorIcon)
            lTreeView.AppendColumn(lIconColumn)
            
            ' Type column
            Dim lTypeRenderer As New CellRendererText()
            lTreeView.AppendColumn("Type", lTypeRenderer, "text", 0)
            
            ' Code column
            Dim lCodeRenderer As New CellRendererText()
            lTreeView.AppendColumn("code", lCodeRenderer, "text", 1)
            
            ' Description column
            Dim lDescRenderer As New CellRendererText()
            lTreeView.AppendColumn("Description", lDescRenderer, "text", 2)
            
            ' File column
            Dim lFileRenderer As New CellRendererText()
            lTreeView.AppendColumn("File", lFileRenderer, "text", 3)
            
            ' Line column
            Dim lLineRenderer As New CellRendererText()
            lTreeView.AppendColumn("Line", lLineRenderer, "text", 4)
            
            ' Column column
            Dim lColumnRenderer As New CellRendererText()
            lTreeView.AppendColumn("Column", lColumnRenderer, "text", 5)
            
            ' Handle row activation
            AddHandler lTreeView.RowActivated, AddressOf OnErrorListRowActivated
            
            Return lTreeView
            
        Catch ex As Exception
            Console.WriteLine($"CreateErrorListView error: {ex.Message}")
            Return New TreeView()
        End Try
    End Function
    

    
    ' Clear error list
    Public Sub ClearErrorList()
        Try
            If pErrorListView IsNot Nothing Then
                Dim lStore As ListStore = CType(pErrorListView.Model, ListStore)
                lStore?.Clear()
            End If
        Catch ex As Exception
            Console.WriteLine($"ClearErrorList error: {ex.Message}")
        End Try
    End Sub
    
    ' Add error to error list
    Public Sub AddError(vType As String, vCode As String, vDescription As String, 
                       vFile As String, vLine As Integer, vColumn As Integer)
        Try
            If pErrorListView IsNot Nothing Then
                Dim lStore As ListStore = CType(pErrorListView.Model, ListStore)
                lStore?.AppendValues(vType, vCode, vDescription, vFile, vLine, vColumn)
            End If
        Catch ex As Exception
            Console.WriteLine($"AddError error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle TODO selection
    Private Sub OnTodoSelected(vFile As String, vLine As Integer, vText As String)
        Try
            If Not String.IsNullOrEmpty(vFile) Then
                OpenSpecificFile(vFile, vLine, 1)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnTodoSelected error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle build error double-click
    Private Sub OnBuildErrorDoubleClicked(vError As BuildError)
        Try
            If vError IsNot Nothing AndAlso Not String.IsNullOrEmpty(vError.FilePath) Then
                OpenSpecificFile(vError.FilePath, vError.Line, vError.Column)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnBuildErrorDoubleClicked error: {ex.Message}")
        End Try
    End Sub
    
    ' Send build errors to AI
    Private Sub OnSendBuildErrorsToAI(vErrorsText As String)
        Try
            ' TODO: Implement AI integration
            Console.WriteLine("Send to AI: " & vErrorsText)
            ShowInfo("AI Integration", "AI integration is not yet implemented.")
        Catch ex As Exception
            Console.WriteLine($"OnSendBuildErrorsToAI error: {ex.Message}")
        End Try
    End Sub
    
    ' Render error icon in tree view
    Private Function RenderErrorIcon(vColumn As TreeViewColumn, vRenderer As CellRenderer, vModel As ITreeModel, vIter As TreeIter) As Boolean
        Try
            ' This would typically set an icon based on error severity
            ' For now, just return True to indicate we handled it
            Return True
        Catch ex As Exception
            Console.WriteLine($"RenderErrorIcon error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Handle error list row activation
    Private Sub OnErrorListRowActivated(vSender As Object, vArgs As RowActivatedArgs)
        Try
            ' Get the path and column from the args
            Dim lPath As TreePath = vArgs.Path
            Dim lColumn As TreeViewColumn = vArgs.Column
            
            ' Process the activation
            Console.WriteLine("Error list row activated")
            ' Add actual implementation here if needed
        Catch ex As Exception
            Console.WriteLine($"OnErrorListRowActivated error: {ex.Message}")
        End Try
    End Sub

    Private Sub ShowReplacePanel()
        Try
            ' Show find panel in replace mode
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ShowTab(BottomPanelManager.BottomPanelTab.eFindResults)
            End If
        Catch ex As Exception
            Console.WriteLine($"ShowReplacePanel error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Called when user interacts with the bottom panel
    ''' Cancels the auto-hide timer to prevent hiding while user is working
    ''' </summary>
    Private Sub OnBottomPanelInteraction()
        Try
            ' Cancel auto-hide timer if it's running
            If pAutoHideTimerId > 0 Then
                CancelAutoHideBottomPanelTimer()
                Console.WriteLine("User interaction detected - cancelled auto-hide")
            End If
        Catch ex As Exception
            Console.WriteLine($"OnBottomPanelInteraction error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced ShowBottomPanel that cancels auto-hide timer
    ''' </summary>
    Public Sub ShowBottomPanelEnhanced(Optional vTabIndex As Integer = -1)
        Try
            ' Cancel any pending auto-hide
            CancelAutoHideBottomPanelTimer()
            
            ' Show the panel
            ShowBottomPanel(vTabIndex)
            
        Catch ex As Exception
            Console.WriteLine($"ShowBottomPanelEnhanced error: {ex.Message}")
        End Try
    End Sub
    
End Class