' Managers/BottomPanelManager.vb - Manages the bottom panel tabs
Imports Gtk
Imports System
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Namespace Managers
    Public Class BottomPanelManager
        
        ' Private fields
        Private pNotebook As Notebook
        Private pCurrentHeight As Integer = 200
        Private pSettingsManager As SettingsManager
        Private pIsVisible As Boolean = False
        Private pProjectRoot As String
        
        ' Tab panels
        Private pBuildOutputPanel As BuildOutputPanel
        Private pFindPanel As FindReplacePanel
        Private pTodoPanel As TodoPanel
        Private pAIAssistantPanel As AIAssistantPanel
        Private pHelpViewerPanel As HelpViewerPanel
        Private pGitPanel As GitPanel
        Private pConsoleTextView As TextView
        
        ' Events
        Public Event TabChanged(vTabIndex As Integer)
        Public Event PanelClosed()
        Public Event FindResultSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event TodoSelected(vTodo As TODOItem)
        Public Event BuildErrorSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event SendErrorsToAI(vErrorsText As String)
        Public Event ErrorDoubleClicked(vError As BuildError)
        Public Event HelpTitleChanged(vTitle As String)


        
        ' Properties
        Public ReadOnly Property ErrorListView() As TreeView
            Get
                Return pBuildOutputPanel.ErrorListView
            End Get
        End Property

        Public ReadOnly Property BuildOutputPanel As BuildOutputPanel
            Get
                Return pBuildOutputPanel
            End Get
        End Property
        
        Public ReadOnly Property FindPanel As FindReplacePanel
            Get
                Return pFindPanel
            End Get
        End Property
        
        Public ReadOnly Property TodoPanel As TodoPanel
            Get
                Return pTodoPanel
            End Get
        End Property
        
        Public ReadOnly Property AIAssistantPanel As AIAssistantPanel
            Get
                Return pAIAssistantPanel
            End Get
        End Property
        
        Public ReadOnly Property HelpViewerPanel As HelpViewerPanel
            Get
                Return pHelpViewerPanel
            End Get
        End Property
        
        Public ReadOnly Property GitPanel As GitPanel
            Get
                Return pGitPanel
            End Get
        End Property
        
        Public ReadOnly Property ConsoleTextView As TextView
            Get
                Return pConsoleTextView
            End Get
        End Property
        
        Public Property CurrentHeight As Integer
            Get
                Return pCurrentHeight
            End Get
            Set(Value As Integer)
                pCurrentHeight = Value
            End Set
        End Property
        
        Public Property IsVisible As Boolean
            Get
                Return pIsVisible
            End Get
            Set(Value As Boolean)
                pIsVisible = Value
                If Value Then
                    Show()
                Else
                    Hide()
                End If
            End Set
        End Property        
        
        ' Constructor
        Public Sub New(vSettingsManager As SettingsManager)
            pSettingsManager = vSettingsManager
        End Sub
        
        ' Initialize the bottom panel
        Public Sub Initialize()
            Try
                ' Create notebook
                pNotebook = New Notebook()
                pNotebook.Scrollable = True
                pNotebook.EnablePopup = True
                
                ' Create tabs
                CreateBuildOutputTab()
                CreateFindResultsTab()
                CreateTodoListTab()
                CreateAIAssistantTab()
                CreateHelpViewerTab()
                CreateGitTab()
                CreateConsoleTab()
                
                ' Handle tab switching
                AddHandler pNotebook.SwitchPage, AddressOf OnTabSwitched
                
                ' Initially hide the panel
                pNotebook.Visible = False
                pIsVisible = False
                
            Catch ex As Exception
                Console.WriteLine($"BottomPanelManager.Initialize error: {ex.Message}")
            End Try
        End Sub
        
        ' Create Build Output tab
        Private Sub CreateBuildOutputTab()
            Try
                pBuildOutputPanel = New BuildOutputPanel()
                
                ' Connect events - FIXED: Use proper delegate syntax
                AddHandler pBuildOutputPanel.ErrorSelected, 
                    Sub(vPath As String, vLine As Integer, vCol As Integer)
                        RaiseEvent BuildErrorSelected(vPath, vLine, vCol)
                    End Sub

                AddHandler pBuildOutputPanel.ErrorDoubleClicked, 
                    Sub(vError As BuildError)
                        RaiseEvent ErrorDoubleClicked(vError)
                    End Sub
                
                AddHandler pBuildOutputPanel.SendErrorsToAI, 
                    Sub(vErrors As String)
                        RaiseEvent SendErrorsToAI(vErrors)
                    End Sub
                
                AddHandler pBuildOutputPanel.CloseRequested, 
                    Sub()
                        HidePanel()
                    End Sub
                
                ' Create tab with close button - FIXED: Pass delegate properly
                Dim lTabLabel As Widget = CreateTabWithCloseButton("Build output", 
                    Sub() 
                        HidePanel()
                    End Sub)
                
                pNotebook.AppendPage(pBuildOutputPanel, lTabLabel)
                
            Catch ex As Exception
                Console.WriteLine($"CreateBuildOutputTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Create Find Results tab
        Private Sub CreateFindResultsTab()
            Try
                pFindPanel = New FindReplacePanel()
                
                ' Connect events - FIXED: Use proper delegate syntax
                AddHandler pFindPanel.ResultSelected, 
                    Sub(vPath As String, vLine As Integer, vCol As Integer)
                        RaiseEvent FindResultSelected(vPath, vLine, vCol)
                    End Sub
                
                AddHandler pFindPanel.CloseRequested, 
                    Sub()
                        HidePanel()
                    End Sub
                
                ' Find panel has its own close button, so just use simple label
                pNotebook.AppendPage(pFindPanel, New Label("Find Results"))
                
            Catch ex As Exception
                Console.WriteLine($"CreateFindResultsTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Update build output - PUBLIC METHOD
        Public Sub UpdateBuildOutput(vText As String, vIsError As Boolean)
            Try
                If pBuildOutputPanel IsNot Nothing Then
                    ' FIXED: Use Function delegate instead of Action
                    GLib.Idle.Add(Function()
                        ' FIXED: Use AppendOutput method that exists in BuildOutputPanel
                        pBuildOutputPanel.AppendOutput(vText, If(vIsError, "error", "normal"))
                        Return False
                    End Function)
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateBuildOutput error: {ex.Message}")
            End Try
        End Sub
        
        ' Create TODO List tab
        Private Sub CreateTodoListTab()
            Try
                pTodoPanel = New TodoPanel()
                
                ' Connect events - FIXED: Use proper delegate syntax
                AddHandler pTodoPanel.TodoSelected, 
                    Sub(vTodo As TODOItem)
                        RaiseEvent TodoSelected(vTodo)
                    End Sub
                
                ' Create tab with close button - FIXED: Pass delegate properly
                Dim lTabLabel As Widget = CreateTabWithCloseButton("TODO List", 
                    Sub() 
                        HidePanel()
                    End Sub)
                
                pNotebook.AppendPage(pTodoPanel, lTabLabel)
                
            Catch ex As Exception
                Console.WriteLine($"CreateTodoListTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Create AI Assistant tab
        Private Sub CreateAIAssistantTab()
            Try
                ' FIXED: Get API key from settings manager
                Dim lApiKey As String = If(pSettingsManager IsNot Nothing, 
                    pSettingsManager.GetString("AI.ApiKey", ""), "")
                    
                pAIAssistantPanel = New AIAssistantPanel(lApiKey)
                
                ' Create tab with close button - FIXED: Pass delegate properly
                Dim lTabLabel As Widget = CreateTabWithCloseButton("AI Assistant", 
                    Sub() 
                        HidePanel()
                    End Sub)
                
                pNotebook.AppendPage(pAIAssistantPanel, lTabLabel)
                
            Catch ex As Exception
                Console.WriteLine($"CreateAIAssistantTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Update selected tab - PUBLIC METHOD
        Public Sub UpdateSelectedTab(vText As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pNotebook IsNot Nothing AndAlso pNotebook.CurrentPage >= 0 Then
                        ' Update the current tab based on its type
                        Select Case pNotebook.CurrentPage
                            Case 0 ' Build output
                                If pBuildOutputPanel IsNot Nothing Then
                                    ' FIXED: Use AppendOutput method
                                    pBuildOutputPanel.AppendOutput(vText, "normal")
                                End If
                            Case 6 ' Console
                                If pConsoleTextView IsNot Nothing Then
                                    AppendToConsole(vText)
                                End If
                        End Select
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateSelectedTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Create Help Viewer tab
        Private Sub CreateHelpViewerTab()
            Try
                pHelpViewerPanel = New HelpViewerPanel()
                
                ' Create tab with close button - FIXED: Pass delegate properly
                Dim lTabLabel As Widget = CreateTabWithCloseButton("Help", 
                    Sub() 
                        HidePanel()
                    End Sub)
                    
                ' Add event handlers
                AddHandler pHelpViewerPanel.TitleChanged, AddressOf OnHelpTitleChanged

                pNotebook.AppendPage(pHelpViewerPanel, lTabLabel)
                
            Catch ex As Exception
                Console.WriteLine($"CreateHelpViewerTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Update help title - PUBLIC METHOD
        Public Sub UpdateHelpTitle(vTitle As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pHelpViewerPanel IsNot Nothing Then
                        ' Update the help viewer title
                        RaiseEvent HelpTitleChanged(vTitle)
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateHelpTitle error: {ex.Message}")
            End Try
        End Sub
        
        ' Create Git tab
        Private Sub CreateGitTab()
            Try
                pGitPanel = New GitPanel()
                
                ' Create tab with close button - FIXED: Pass delegate properly
                Dim lTabLabel As Widget = CreateTabWithCloseButton("git", 
                    Sub() 
                        HidePanel()
                    End Sub)
                
                pNotebook.AppendPage(pGitPanel, lTabLabel)
               
            Catch ex As Exception
                Console.WriteLine($"CreateGitTab error: {ex.Message}")
            End Try
        End Sub

        ' Set project root
        Public Sub SetProjectRoot(vProjectRoot As String)
            Try
                If Not String.IsNullOrEmpty(vProjectRoot) Then
                    pProjectRoot = vProjectRoot
                    pGitPanel.ProjectRoot = System.IO.Path.GetDirectoryName(vProjectRoot)
                End If
            Catch ex As Exception
                Console.WriteLine($"BottomPanelManager.SetProjectRoot error: {ex.Message}")
            End Try
        End Sub
        
        ' Create Console tab
        Private Sub CreateConsoleTab()
            Try
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                pConsoleTextView = New TextView()
                pConsoleTextView.Editable = False
                pConsoleTextView.WrapMode = WrapMode.Word
                
                ' Apply font settings
                If pSettingsManager IsNot Nothing Then
                    Dim lFontDesc As String = pSettingsManager.EditorFont
                    ' FIXED: Use correct CssHelper method
                    If Not String.IsNullOrEmpty(lFontDesc) Then
                        Dim lCss As String = CssHelper.GenerateTextViewFontCss(lFontDesc)
                        CssHelper.ApplyCssToWidget(pConsoleTextView, lCss, 
                            CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                    End If
                End If
                
                lScrolled.Add(pConsoleTextView)
                
                ' Create tab with close button - FIXED: Pass delegate properly
                Dim lTabLabel As Widget = CreateTabWithCloseButton("Console", 
                    Sub() 
                        HidePanel()
                    End Sub)
                
                pNotebook.AppendPage(lScrolled, lTabLabel)
                
            Catch ex As Exception
                Console.WriteLine($"CreateConsoleTab error: {ex.Message}")
            End Try
        End Sub
        
        ' ADDITIONAL PUBLIC METHODS that might be causing errors
        
        ' Process build complete
        Public Sub ProcessBuildComplete(vSuccess As Boolean)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pBuildOutputPanel IsNot Nothing Then
                        Dim lMessage As String = If(vSuccess, "Build completed successfully", "Build failed")
                        ' FIXED: Use AppendOutput method
                        pBuildOutputPanel.AppendOutput(lMessage & Environment.NewLine, If(vSuccess, "Success", "error"))
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"ProcessBuildComplete error: {ex.Message}")
            End Try
        End Sub
        
        ' Update find results
        Public Sub UpdateFindResults(vResults As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pFindPanel IsNot Nothing Then
                        ' Update find panel with results
                        ' This would need to be implemented in FindReplacePanel
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateFindResults error: {ex.Message}")
            End Try
        End Sub
        
        ' Update git status
        Public Sub UpdateGitStatus(vStatus As String)
            Try
                ' FIXED: Use Function delegate instead of Action
                GLib.Idle.Add(Function()
                    If pGitPanel IsNot Nothing Then
                        ' Update git panel status
                        ' This would need to be implemented in GitPanel
                    End If
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"UpdateGitStatus error: {ex.Message}")
            End Try
        End Sub
        
        ' Create tab with close button helper - FIXED: Changed parameter type
        Private Function CreateTabWithCloseButton(vLabelText As String, vCloseAction As System.Action) As Widget
            Try
                Dim lBox As New Box(Orientation.Horizontal, 4)
                
                ' Title label
                Dim lLabel As New Label(vLabelText)
                lBox.PackStart(lLabel, True, True, 0)
                
                ' Close button
                Dim lCloseButton As New Button()
                lCloseButton.Relief = ReliefStyle.None
                lCloseButton.FocusOnClick = False
                
                ' Create close icon
                Dim lCloseIcon As New Image()
                lCloseIcon.IconName = "window-close"
                lCloseIcon.IconSize = CInt(IconSize.Menu)
                lCloseButton.Add(lCloseIcon)
                
                ' Make button smaller
                lCloseButton.SetSizeRequest(18, 18)
                
                ' Connect close action - FIXED: Call the delegate directly
                AddHandler lCloseButton.Clicked, 
                    Sub(sender, e) 
                        vCloseAction.Invoke()
                    End Sub
                    
                lBox.PackStart(lCloseButton, False, False, 0)
                
                lBox.ShowAll()
                Return lBox
                
            Catch ex As Exception
                Console.WriteLine($"CreateTabWithCloseButton error: {ex.Message}")
                Return New Label(vLabelText)
            End Try
        End Function

        Public Sub ShowConsole()
            ShowTab(6)
        End Sub
        
        ' Show specific tab
        Public Sub ShowTab(vTabIndex As Integer)
            Try
                If vTabIndex >= 0 AndAlso vTabIndex < pNotebook.NPages Then
                    pNotebook.CurrentPage = vTabIndex
                    IsVisible = True
                End If
            Catch ex As Exception
                Console.WriteLine($"ShowTab error: {ex.Message}")
            End Try
        End Sub
        
        ' Show tab by type
        Public Sub ShowTabByType(vTabType As BottomPanelTab)
            Try
                Dim lIndex As Integer = CInt(vTabType)
                ShowTab(lIndex)
            Catch ex As Exception
                Console.WriteLine($"ShowTabByType error: {ex.Message}")
            End Try
        End Sub
        
        ' Hide the panel
        Public Sub HidePanel()
            Try
                IsVisible = False
                RaiseEvent PanelClosed()
            Catch ex As Exception
                Console.WriteLine($"HidePanel error: {ex.Message}")
            End Try
        End Sub

        Public Sub ToggleVisible()
            If IsVisible Then 
                HidePanel()
            Else
                ShowTab(pNotebook.CurrentPage)
            End If
        End Sub
        
        ' Clear console
        Public Sub ClearConsole()
            Try
                If pConsoleTextView IsNot Nothing Then
                    pConsoleTextView.Buffer.Clear()
                End If
            Catch ex As Exception
                Console.WriteLine($"ClearConsole error: {ex.Message}")
            End Try
        End Sub
        
        ' Append text to console
        Public Sub AppendToConsole(vText As String)
            Try
                If pConsoleTextView IsNot Nothing Then
                    Dim lBuffer As TextBuffer = pConsoleTextView.Buffer
                    Dim lEndIter As TextIter = lBuffer.EndIter
                    lBuffer.PlaceCursor(lEndIter)
                    lBuffer.InsertAtCursor(vText)
                    
                    ' Scroll to bottom
                    pConsoleTextView.ScrollToIter(lBuffer.EndIter, 0, False, 0, 0)
                End If
            Catch ex As Exception
                Console.WriteLine($"AppendToConsole error: {ex.Message}")
            End Try
        End Sub
        
        ' Update project root for panels that need it
        Public Sub UpdateProjectRoot(vProjectRoot As String)
            Try
                pFindPanel?.SetProjectRoot(vProjectRoot)
                pTodoPanel?.SetProjectRoot(vProjectRoot)
                pGitPanel?.SetProjectRoot(vProjectRoot)
            Catch ex As Exception
                Console.WriteLine($"UpdateProjectRoot error: {ex.Message}")
            End Try
        End Sub
        
        ' Event handler for tab switching
        Private Sub OnTabSwitched(vSender As Object, vArgs As SwitchPageArgs)
            Try
                RaiseEvent TabChanged(CInt(vArgs.PageNum))
                
                ' Focus appropriate widget based on tab
                Select Case vArgs.PageNum
                    Case 0 ' Build output
                        pBuildOutputPanel?.GrabFocus()
                    Case 1 ' Find Results
                        pFindPanel?.GrabFocus()
                    Case 2 ' TODO List
                        pTodoPanel?.GrabFocus()
                    Case 3 ' AI Assistant
                        pAIAssistantPanel?.GrabFocus()
                    Case 4 ' Help Viewer
                        pHelpViewerPanel?.GrabFocus()
                    Case 5 ' git
                        pGitPanel?.GrabFocus()
                    Case 6 ' Console
                        pConsoleTextView?.GrabFocus()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnTabSwitched error: {ex.Message}")
            End Try
        End Sub
        
        ' Copy errors to clipboard
        Public Sub CopyErrorsToClipboard()
            If pBuildOutputPanel IsNot Nothing Then
                pBuildOutputPanel.CopyErrorsToClipboard()
            End If
        End Sub
        
        ' Enum for tab types
        Public Enum BottomPanelTab
            eBuildOutput = 0
            eFindResults = 1
            eTodoList = 2
            eAIAssistant = 3
            eHelpViewer = 4
            eGit = 5
            eConsole = 6
        End Enum

        Private Sub OnHelpTitleChanged(vTitle As String)
            Try
                ' Update window title or status bar with help title
                RaiseEvent HelpTitleChanged(vTitle)
                Console.WriteLine($"Help Title changed: {vTitle}")
            Catch ex As Exception
                Console.WriteLine($"OnHelpTitleChanged error: {ex.Message}")
            End Try
        End Sub

        ' Get the widget to add to the UI (for packing into paned)
        Public Function GetWidget() As Widget
            Return pNotebook
        End Function
        
        ' Set NoShowAll property for initialization
        Public Sub SetNoShowAll(vValue As Boolean)
            If pNotebook IsNot Nothing Then
                pNotebook.NoShowAll = vValue
            End If
        End Sub
        
        ' Show the notebook widget
        Public Sub Show()
            If pNotebook IsNot Nothing Then
                pNotebook.NoShowAll = False
                pNotebook.Show()
            End If
        End Sub
        
        ' Hide the notebook widget
        Public Sub Hide()
            If pNotebook IsNot Nothing Then
                pNotebook.Hide()
                pNotebook.NoShowAll = True
            End If
        End Sub
        
        ' Find tab index for a specific panel widget
        Public Function GetTabIndexForPanel(vPanel As Widget) As Integer
            Try
                If pNotebook IsNot Nothing AndAlso vPanel IsNot Nothing Then
                    For i As Integer = 0 To pNotebook.NPages - 1
                        If pNotebook.GetNthPage(i) Is vPanel Then
                            Return i
                        End If
                    Next
                End If
                Return -1
            Catch ex As Exception
                Console.WriteLine($"GetTabIndexForPanel error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ' Show tab containing specific panel
        Public Sub ShowTabForPanel(vPanel As Widget)
            Try
                Dim lIndex As Integer = GetTabIndexForPanel(vPanel)
                If lIndex >= 0 Then
                    ShowTab(lIndex)
                End If
            Catch ex As Exception
                Console.WriteLine($"ShowTabForPanel error: {ex.Message}")
            End Try
        End Sub
        
        ' Update tab label text
        Public Sub SetTabLabelText(vPanel As Widget, vText As String)
            Try
                If pNotebook IsNot Nothing AndAlso vPanel IsNot Nothing Then
                    Dim lPageNum As Integer = pNotebook.PageNum(vPanel)
                    If lPageNum >= 0 Then
                        pNotebook.SetTabLabelText(vPanel, vText)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"SetTabLabelText error: {ex.Message}")
            End Try
        End Sub
        
        ' Get current page index
        Public Function GetCurrentPageIndex() As Integer
            If pNotebook IsNot Nothing Then
                Return pNotebook.CurrentPage
            End If
            Return -1
        End Function
        
'        Public Sub UpdateBuildTabLabel(vErrorCount As Integer, vWarningCount As Integer)
'            Try
'                If pBuildOutputPanel IsNot Nothing AndAlso pNotebook IsNot Nothing Then
'                    Dim lPageNum As Integer = pNotebook.PageNum(pBuildOutputPanel)
'                    If lPageNum >= 0 Then
'                        Dim lText As String
'                        If vErrorCount > 0 OrElse vWarningCount > 0 Then
'                            lText = $"Build Output ({vErrorCount} errors, {vWarningCount} warnings)"
'                        Else
'                            lText = "Build Output"
'                        End If
'                        pNotebook.SetTabLabelText(pBuildOutputPanel, lText)
'                    End If
'                End If
'            Catch ex As Exception
'                Console.WriteLine($"UpdateBuildTabLabel error: {ex.Message}")
'            End Try
'        End Sub

        ' Update Build Output tab label with error/warning counts without losing close button
        Public Sub UpdateBuildTabLabel(vErrorCount As Integer, vWarningCount As Integer)
            Try
                If pBuildOutputPanel IsNot Nothing AndAlso pNotebook IsNot Nothing Then
                    Dim lPageNum As Integer = pNotebook.PageNum(pBuildOutputPanel)
                    If lPageNum >= 0 Then
                        ' Get the current tab label widget (should be a Box)
                        Dim lTabWidget As Widget = pNotebook.GetTabLabel(pBuildOutputPanel)
                        
                        If TypeOf lTabWidget Is Box Then
                            Dim lBox As Box = CType(lTabWidget, Box)
                            
                            ' Find the Label within the Box
                            For Each lChild As Widget In lBox.Children
                                If TypeOf lChild Is Label Then
                                    Dim lLabel As Label = CType(lChild, Label)
                                    
                                    ' Update just the text
                                    If vErrorCount > 0 OrElse vWarningCount > 0 Then
                                        lLabel.Text = $"Build output ({vErrorCount} Errors, {vWarningCount} Warnings)"
                                    Else
                                        lLabel.Text = "Build output"
                                    End If
                                    Exit For
                                End If
                            Next
                        End If
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateBuildTabLabel error: {ex.Message}")
            End Try
        End Sub

        ' Toggle output panel visibility
        Public Sub ToggleOutputPanel()
            Try
                If pIsVisible Then
                    If pNotebook.CurrentPage = CInt(BottomPanelTab.eBuildOutput) Then
                        HidePanel()
                    Else
                        ShowTab(BottomPanelTab.eBuildOutput)
                    End If
                Else
                    Show()
                    ShowTab(BottomPanelTab.eBuildOutput)
                End If
            Catch ex As Exception
                Console.WriteLine($"ToggleOutputPanel error: {ex.Message}")
            End Try
        End Sub

        ' Toggle error list panel visibility
        Public Sub ToggleErrorListPanel()
            Try
                If pIsVisible Then
                    If pNotebook.CurrentPage = CInt(BottomPanelTab.eBuildOutput) Then
                        HidePanel()
                    Else
                        ShowTab(BottomPanelTab.eBuildOutput)
                    End If
                Else
                    Show()
                    ShowTab(BottomPanelTab.eBuildOutput)
                End If
            Catch ex As Exception
                Console.WriteLine($"ToggleErrorListPanel error: {ex.Message}")
            End Try
        End Sub

    End Class
End Namespace
