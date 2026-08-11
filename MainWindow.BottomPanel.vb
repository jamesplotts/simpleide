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

    ''' <summary>
    ''' Gets the error list data grid from the bottom panel manager
    ''' </summary>
    Private ReadOnly Property pErrorListView() As CustomDrawDataGrid
        Get
            Return pBottomPanelManager?.ErrorListView
        End Get
    End Property
    
    ' Show bottom panel with specific tab
    Public Sub ShowBottomPanel(Optional vTabIndex As Integer = -1)
        Try
            UpdatePanedConstraints()

            ' Set the target position BEFORE the bottom panel's second child becomes
            ' visible, while pCenterVPaned.AllocatedHeight still reflects a valid,
            ' already-settled layout - the editor pane was simply using the paned's whole
            ' height while the bottom panel was hidden, so that height IS the correct
            ' "total" post-split height too. Without this, GTK allocates the newly-shown
            ' second child its full natural size for the first frame it participates in a
            ' real size-allocate, and only the NEXT allocate (the fallback below) snaps it
            ' down - visible as an "opens large, then shrinks" flash. Pre-setting the
            ' position here avoids that natural-size allocation happening at all in the
            ' common case (the main window has already been laid out at least once).
            If pCenterVPaned IsNot Nothing AndAlso pCenterVPaned.AllocatedHeight > 0 Then
                ApplyDefaultBottomPanelPosition(pCenterVPaned.AllocatedHeight)
            End If

            pBottomPanelManager.Show()
            pBottomPanelManager.ShowTab(vTabIndex)
            pBottomPanelVisible = True

            ' Fallback for when the pre-set above couldn't run (AllocatedHeight not valid
            ' yet, e.g. showing the bottom panel during startup before the main window's
            ' first layout pass) - flag that a reset is wanted and let
            ' OnCenterVPanedSizeAllocated (already fired on every real allocation) apply it
            ' once against a genuinely current AllocatedHeight.
            pApplyDefaultBottomPanelPositionOnNextAllocate = True
            If pCenterVPaned IsNot Nothing Then pCenterVPaned.QueueResize()

        Catch ex As Exception
            Console.WriteLine($"ShowBottomPanel error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Computes and applies the default bottom-panel paned position for a given total
    ''' paned height - shared by ShowBottomPanel (set eagerly, before the panel becomes
    ''' visible), OnCenterVPanedSizeAllocated (a fallback for when ShowBottomPanel ran
    ''' before a valid AllocatedHeight existed yet), and UpdatePanelVisibility
    ''' </summary>
    ''' <param name="vTotalHeight">Current total allocated height of pCenterVPaned</param>
    Private Sub ApplyDefaultBottomPanelPosition(vTotalHeight As Integer)
        Try
            If pCenterVPaned Is Nothing OrElse vTotalHeight <= 0 Then Return
            Dim lMaxPosition As Integer = vTotalHeight - 50
            Dim lTargetPosition As Integer = vTotalHeight - BOTTOM_PANEL_HEIGHT
            pCenterVPaned.Position = Math.Max(50, Math.Min(lMaxPosition, lTargetPosition))
        Catch ex As Exception
            Console.WriteLine($"ApplyDefaultBottomPanelPosition error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Hide bottom panel and return focus to the current editor
    ''' </summary>
    ''' <remarks>
    ''' Properly returns focus to the editor after hiding the panel
    ''' </remarks>
    Public Sub HideBottomPanel()
        Try
            pBottomPanelManager.HidePanel()
            pBottomPanelManager.Hide()
            pBottomPanelVisible = False
            pApplyDefaultBottomPanelPositionOnNextAllocate = False

            ' Escape (MainWindow.Keyboard.vb) calls this Sub directly, bypassing the View
            ' menu's Bottom Panel checkbox - without this it goes stale relative to the
            ' panel's real visibility
            SyncViewMenuCheckbox(pBottomPanelMenuItem, pBottomPanelVisible)

            ' Return focus to editor if available
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                ' Use GrabFocus directly on the editor
                lEditor.GrabFocus()
                #If DEBUG Then
                Console.WriteLine("Focus returned to editor after hiding bottom panel")
                #End If
            Else
                #If DEBUG Then
                Console.WriteLine("No editor available to focus after hiding bottom panel")
                #End If
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
            ' Pass theme manager to bottom panel manager
            If pBottomPanelManager IsNot Nothing AndAlso pThemeManager IsNot Nothing Then
                pBottomPanelManager.SetThemeManager(pThemeManager)
            End If
            InitializePanedConstraints()
            InitializeBuildOutputTheme()
            InitializeBottomPanelTheme()
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
    

    
    ''' <summary>
    ''' Clears the error list in the build output panel
    ''' </summary>
    Public Sub ClearErrorList()
        Try
            If pBottomPanelManager?.BuildOutputPanel IsNot Nothing Then
                pBottomPanelManager.BuildOutputPanel.ClearOutput()
            End If
        Catch ex As Exception
            Console.WriteLine($"ClearErrorList error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Adds an error or warning to the build output panel's CustomDrawDataGrid
    ''' </summary>
    ''' <param name="vType">Type of issue ("error" or "warning")</param>
    ''' <param name="vCode">Error or warning code (e.g., "BC30451")</param>
    ''' <param name="vDescription">Description of the issue</param>
    ''' <param name="vFile">File path where the issue occurred</param>
    ''' <param name="vLine">Line number where the issue occurred</param>
    ''' <param name="vColumn">Column number where the issue occurred</param>
    Public Sub AddError(vType As String, vCode As String, vDescription As String, 
                       vFile As String, vLine As Integer, vColumn As Integer)
        Try
            ' Ensure build output panel exists
            If pBuildOutputPanel Is Nothing Then
                #If DEBUG Then
                Console.WriteLine("BuildOutputPanel not initialized")
                #End If
                Return
            End If
            
            ' Determine which grid to add to based on type
            Dim lTargetGrid As CustomDrawDataGrid = Nothing
            Dim lIsError As Boolean = False
            
            Select Case vType.ToLower()
                Case "error"
                    lTargetGrid = pBuildOutputPanel.ErrorsDataGrid
                    lIsError = True
                Case "warning"
                    lTargetGrid = pBuildOutputPanel.WarningsDataGrid
                    lIsError = False
                Case Else
                    ' Default to error if type is unknown
                    lTargetGrid = pBuildOutputPanel.ErrorsDataGrid
                    lIsError = True
            End Select
            
            ' Check if the grid exists
            If lTargetGrid Is Nothing Then
                #If DEBUG Then
                Console.WriteLine($"Target grid for {vType} not available")
                #End If
                Return
            End If
            
            ' Create a new DataGridRow
            Dim lRow As New DataGridRow()
            
            ' Set row style based on type
            If lIsError Then
                lRow.Style = RowStyle.eError
                
                ' Create and store BuildError in Tag for later retrieval
                Dim lBuildError As New BuildError()
                lBuildError.FilePath = vFile
                lBuildError.Line = vLine
                lBuildError.Column = vColumn
                lBuildError.ErrorCode = vCode
                lBuildError.Message = vDescription
                lRow.Tag = lBuildError
            Else
                lRow.Style = RowStyle.eWarning
                
                ' Create and store BuildWarning in Tag for later retrieval
                Dim lBuildWarning As New BuildWarning()
                lBuildWarning.FilePath = vFile
                lBuildWarning.Line = vLine
                lBuildWarning.Column = vColumn
                lBuildWarning.WarningCode = vCode
                lBuildWarning.Message = vDescription
                lRow.Tag = lBuildWarning
            End If
            
            ' Add cells to the row
            ' Note: The grid expects 6 columns based on ErrorColumns/WarningColumns enum:
            ' Icon, File, Line, Column, Code, Message
            
            ' Icon cell (column 0)
            Dim lIconCell As New DataGridCell()
            lIconCell.Value = vType.ToLower()  ' "error" or "warning"
            lIconCell.DisplayText = ""  ' Icon will be drawn by the renderer
            If lIsError Then
                lIconCell.ForegroundColor = "#FF0000"  ' Red for errors
            Else
                lIconCell.ForegroundColor = "#FFA500"  ' Orange for warnings
            End If
            lRow.Cells.Add(lIconCell)
            
            ' File cell (column 1)
            Dim lFileCell As New DataGridCell()
            lFileCell.Value = System.IO.Path.GetFileName(vFile)  ' Show just filename
            lFileCell.DisplayText = System.IO.Path.GetFileName(vFile)
            lFileCell.ToolTip = vFile  ' Full path in tooltip
            lRow.Cells.Add(lFileCell)
            
            ' Line cell (column 2)
            Dim lLineCell As New DataGridCell()
            lLineCell.Value = vLine
            lLineCell.DisplayText = vLine.ToString()
            lRow.Cells.Add(lLineCell)
            
            ' Column cell (column 3)
            Dim lColumnCell As New DataGridCell()
            lColumnCell.Value = vColumn
            lColumnCell.DisplayText = vColumn.ToString()
            lRow.Cells.Add(lColumnCell)
            
            ' Code cell (column 4)
            Dim lCodeCell As New DataGridCell()
            lCodeCell.Value = vCode
            lCodeCell.DisplayText = vCode
            lRow.Cells.Add(lCodeCell)
            
            ' Message cell (column 5)
            Dim lMessageCell As New DataGridCell()
            lMessageCell.Value = vDescription
            lMessageCell.DisplayText = vDescription
            lMessageCell.ToolTip = vDescription  ' Full message in tooltip for long messages
            lRow.Cells.Add(lMessageCell)
            
            ' Add the row to the grid
            lTargetGrid.AddRow(lRow)
            
            ' Update tab labels to show counts
            If lIsError Then
                ' Update errors tab label
                Dim lErrorCount As Integer = lTargetGrid.Rows.Count
                pBuildOutputPanel.Notebook.SetTabLabel(1, $"<span foreground='red'>Errors ({lErrorCount})</span>" )
            Else
                ' Update warnings tab label
                Dim lWarningCount as Integer = lTargetGrid.Rows.Count
                pBuildOutputPanel.Notebook.SetTabLabel(2, $"<span foreground='orange'>Warnings ({lWarningCount})</span>" )
            End If
            
        Catch ex As Exception
            Console.WriteLine($"AddError error: {ex.Message}")
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
            #If DEBUG Then
            Console.WriteLine("Error list row activated")
            #End If
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
    ''' Handles the bottom panel closed event to return focus to editor
    ''' </summary>
    ''' <remarks>
    ''' Called when the bottom panel is closed by any means
    ''' </remarks>
    Private Sub OnBottomPanelClosed()
        Try
            ' Get the current editor
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                ' Schedule focus return on idle to ensure panel is fully hidden
                GLib.Idle.Add(Function()
                    lEditor.GrabFocus()
                    #If DEBUG Then
                    Console.WriteLine("Focus returned to editor via panel closed event")
                    #End If
                    Return False ' Run once
                End Function)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnBottomPanelClosed error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Connects bottom panel events including the closed event
    ''' </summary>
    ''' <remarks>
    ''' Call this during initialization after creating the BottomPanelManager
    ''' </remarks>
    Private Sub ConnectBottomPanelEvents()
        Try
            If pBottomPanelManager IsNot Nothing Then
                ' Connect to panel closed event
                AddHandler pBottomPanelManager.PanelClosed, AddressOf OnBottomPanelClosed
                #If DEBUG Then
                Console.WriteLine("Connected bottom panel events")
                #End If
            End If
        Catch ex As Exception
            Console.WriteLine($"ConnectBottomPanelEvents error: {ex.Message}")
        End Try
    End Sub

    
    ''' <summary>
    ''' Determines if editor should receive focus when window is activated
    ''' </summary>
    ''' <returns>True if editor should be focused, False otherwise</returns>
    ''' <remarks>
    ''' Checks various conditions to determine if editor focus is appropriate
    ''' </remarks>
    Private Function ShouldFocusEditor() As Boolean
        Try
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"ShouldFocusEditor error: {ex.Message}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Initializes paned widget constraints for proper resizing
    ''' </summary>
    Private Sub InitializePanedConstraints()
        Try
            If pCenterVPaned Is Nothing Then Return
            
            ' Connect to realize event to set constraints when widget is ready
            AddHandler pCenterVPaned.Realized, AddressOf OnCenterVPanedRealized
            
            ' Connect to button press/release events for drag handling
            AddHandler pCenterVPaned.ButtonPressEvent, AddressOf OnPanedButtonPress
            AddHandler pCenterVPaned.ButtonReleaseEvent, AddressOf OnPanedButtonRelease
            AddHandler pCenterVPaned.MotionNotifyEvent, AddressOf OnPanedMotionNotify
            
            ' Enable motion events
            pCenterVPaned.Events = pCenterVPaned.Events Or EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or EventMask.PointerMotionMask
            
            
        Catch ex As Exception
            Console.WriteLine($"InitializePanedConstraints error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles the realized event to set initial constraints
    ''' </summary>
    Private Sub OnCenterVPanedRealized(vSender As Object, vArgs As EventArgs)
        Try
            UpdatePanedConstraints()
        Catch ex As Exception
            Console.WriteLine($"OnCenterVPanedRealized error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Updates the paned position constraints based on current allocation
    ''' </summary>
    Private Sub UpdatePanedConstraints()
        Try
            If pCenterVPaned Is Nothing OrElse pCenterVPaned.AllocatedHeight <= 0 Then Return
            
            Dim lTotalHeight As Integer = pCenterVPaned.AllocatedHeight
            
            ' Calculate min/max positions (1/4 and 3/4 of height)
            Dim lMinEditorHeight As Integer = CInt(lTotalHeight * 0.25)  ' Editor gets at least 1/4
            Dim lMaxEditorHeight As Integer = CInt(lTotalHeight * 0.75)  ' Editor gets at most 3/4
            
            ' This means bottom panel height ranges from 1/4 to 3/4
            ' Position is measured from top, so:
            ' Min position = 1/4 of total (bottom panel gets 3/4)
            ' Max position = 3/4 of total (bottom panel gets 1/4)
            
            ' Store constraints for use in position changed handler
            pCenterVPaned.Data("MinPosition") = lMinEditorHeight
            pCenterVPaned.Data("MaxPosition") = lMaxEditorHeight
            
            'Console.WriteLine($"Paned constraints set: Height={lTotalHeight}, MinPos={lMinEditorHeight}, MaxPos={lMaxEditorHeight}")
            
        Catch ex As Exception
            Console.WriteLine($"UpdatePanedConstraints error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Track when paned dragging starts
    ''' </summary>
    Private pIsDraggingPaned As Boolean = False
    
    ''' <summary>
    ''' Handle button press on paned separator
    ''' </summary>
    Private Function OnPanedButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
        Try
            ' Check if click is on the handle/separator
            Dim lPaned As Paned = TryCast(vSender, Paned)
            If lPaned IsNot Nothing Then
                pIsDraggingPaned = True
                #If DEBUG Then
                Console.WriteLine("Started dragging paned separator")
                #End If
            End If
            Return False ' Let default handler process
        Catch ex As Exception
            Console.WriteLine($"OnPanedButtonPress error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ''' <summary>
    ''' Handle button release on paned separator
    ''' </summary>
    Private Function OnPanedButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
        Try
            pIsDraggingPaned = False
            #If DEBUG Then
            Console.WriteLine("Stopped dragging paned separator")
            #End If
            
            
            Return False
        Catch ex As Exception
            Console.WriteLine($"OnPanedButtonRelease error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ''' <summary>
    ''' Handle motion while dragging paned separator
    ''' </summary>
    Private Function OnPanedMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
        Try
            ' Always enforce constraints when the paned is being moved
            GLib.Idle.Add(Function()
                EnforcePanedConstraints()
                Return False ' Run once
            End Function)
            Return False
        Catch ex As Exception
            Console.WriteLine($"OnPanedMotionNotify error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ''' <summary>
    ''' Enforces the min/max constraints on the paned position
    ''' </summary>
    Private Sub EnforcePanedConstraints()
        Try
            If pCenterVPaned Is Nothing OrElse pCenterVPaned.AllocatedHeight <= 0 Then Return
            
            Dim lTotalHeight As Integer = pCenterVPaned.AllocatedHeight
            Dim lCurrentPos As Integer = pCenterVPaned.Position
            
            ' Calculate constraints
            Dim lMinPos As Integer = CInt(lTotalHeight * 0.25)  ' Bottom panel max 3/4
            Dim lMaxPos As Integer = CInt(lTotalHeight * 0.75)  ' Bottom panel min 1/4
            
            ' Apply constraints
            If lCurrentPos < lMinPos Then
                pCenterVPaned.Position = lMinPos
                'Console.WriteLine($"Enforced min position: {lMinPos}")
            ElseIf lCurrentPos > lMaxPos Then
                pCenterVPaned.Position = lMaxPos
                #If DEBUG Then
                Console.WriteLine($"Enforced max position: {lMaxPos}")
                #End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"EnforcePanedConstraints error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Call this from BuildUI after creating pCenterVPaned
    ''' </summary>
    Private Sub SetupPanedHandling()
        Try
            InitializePanedConstraints()
            
            ' Also handle size allocation changes
            AddHandler pCenterVPaned.SizeAllocated, AddressOf OnCenterVPanedSizeAllocated
            
        Catch ex As Exception
            Console.WriteLine($"SetupPanedHandling error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle size allocation changes to update constraints, and apply a pending
    ''' default-position reset (see ShowBottomPanel) once a genuinely current allocation
    ''' is available
    ''' </summary>
    Private Sub OnCenterVPanedSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
        Try
            UpdatePanedConstraints()

            If pApplyDefaultBottomPanelPositionOnNextAllocate AndAlso pCenterVPaned IsNot Nothing AndAlso vArgs.Allocation.Height > 0 Then
                ApplyDefaultBottomPanelPosition(vArgs.Allocation.Height)
                pApplyDefaultBottomPanelPositionOnNextAllocate = False
            End If

        Catch ex As Exception
            Console.WriteLine($"OnCenterVPanedSizeAllocated error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Initializes theme support for the build output panel
    ''' </summary>
    Private Sub InitializeBuildOutputTheme()
        Try
            If pBuildOutputPanel IsNot Nothing AndAlso pThemeManager IsNot Nothing Then
                pBuildOutputPanel.SetThemeManager(pThemeManager)
            End If
        Catch ex As Exception
            Console.WriteLine($"InitializeBuildOutputTheme error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Initializes theme support for the bottom panel manager and its panels
    ''' </summary>
    Private Sub InitializeBottomPanelTheme()
        Try
            If pBottomPanelManager IsNot Nothing AndAlso pThemeManager IsNot Nothing Then
                pBottomPanelManager.SetThemeManager(pThemeManager)
            End If
        Catch ex As Exception
            Console.WriteLine($"InitializeBottomPanelTheme error: {ex.Message}")
        End Try
    End Sub

    
End Class