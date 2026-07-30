' MainWindow.HelpTab.vb - Help browser tab management for MainWindow
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces


Partial Public Class MainWindow
    
    ' ===== Private Fields =====
    Private pHelpTabs As New Dictionary(Of String, TabInfo)()
    Private pNextHelpTabId As Integer = 1
    
    ' ===== Help Tab Management =====
    
    ''' <summary>
    ''' Opens a help browser as a tab in the main notebook
    ''' </summary>
    ''' <param name="vTopic">Optional help topic to navigate to</param>
    ''' <param name="vUrl">Optional URL to navigate to</param>
    ''' <returns>The unique ID of the help tab</returns>
    Public Function OpenHelpTab(Optional vTopic As String = Nothing, Optional vUrl As String = Nothing) As String
        Try
            Console.WriteLine($"OpenHelpTab: Topic='{vTopic}', URL='{vUrl}'")
            
            ' Close welcome tab if it exists
            CloseWelcomeTab()
            
            ' Generate unique ID for this help tab
            Dim lHelpTabId As String = $"help_{pNextHelpTabId}"
            pNextHelpTabId += 1

            ' Create new help browser widget
            Dim lHelpBrowser As HelpBrowser
            
            ' Check if we already have a help tab with this content
            If Not String.IsNullOrEmpty(vUrl) Then
                For Each lKvp In pHelpTabs
                    lHelpBrowser = TryCast(lKvp.Value.EditorContainer, HelpBrowser)
                    If lHelpBrowser IsNot Nothing AndAlso lHelpBrowser.CurrentUrl = vUrl Then
                        ' Switch to existing tab
                        SwitchToHelpTab(lKvp.Key)
                        Return lKvp.Key
                    End If
                Next
            End If
            
            ' Create new help browser widget
            lHelpBrowser = New HelpBrowser(pSettingsManager)
            
            ' Navigate to requested content
            If Not String.IsNullOrEmpty(vUrl) Then
                lHelpBrowser.NavigateToUrl(vUrl)
            ElseIf Not String.IsNullOrEmpty(vTopic) Then
                lHelpBrowser.NavigateToTopic(vTopic)
            Else
                lHelpBrowser.NavigateToHome()
            End If
            
            ' Wire up events
            AddHandler lHelpBrowser.NavigationCompleted, Sub(vCompletedUrl) OnHelpNavigationCompleted(lHelpTabId, vCompletedUrl)
            AddHandler lHelpBrowser.LoadingStateChanged, Sub(vIsLoading) OnHelpLoadingStateChanged(lHelpTabId, vIsLoading)
            
            ' Create tab info
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = $"help:{lHelpTabId}"
            lTabInfo.Editor = Nothing  ' Help tabs don't have editors
            lTabInfo.EditorContainer = lHelpBrowser
            lTabInfo.IsSpecialTab = True
            lTabInfo.Modified = False
            
            ' Create tab label with close button
            Dim lTabTitle As String = If(Not String.IsNullOrEmpty(vTopic), $"Help: {vTopic}", "Help")
            
            ' Add to notebook
            Dim lPageIndex As Integer = pNotebook.AppendPage(lHelpBrowser, lTabTitle)
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Store in dictionary
            pHelpTabs(lHelpTabId) = lTabInfo
            
            ' Update UI
            UpdateStatusBar($"Opened help: {lTabTitle}")
            UpdateToolbarButtons()
            
            Console.WriteLine($"Help tab created with ID: {lHelpTabId}")
            Return lHelpTabId
            
        Catch ex As Exception
            Console.WriteLine($"OpenHelpTab error: {ex.Message}")
            ShowError("Help error", $"Failed To open help: {ex.Message}")
            Return Nothing
        End Try
    End Function
    
    ''' <summary>
    ''' Creates a tab label with title and close button for help tabs
    ''' </summary>
    Private Function CreateHelpTabLabel(vHelpTabId As String, vTitle As String) As Widget
        Try
            Dim lBox As New Box(Orientation.Horizontal, 4)
            
            ' Icon
            Dim lIcon As Image = Image.NewFromIconName("help-browser", IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLabel As New Label(vTitle)
            lLabel.Name = $"help_tab_label_{vHelpTabId}"
            lBox.PackStart(lLabel, True, True, 0)
            
            ' Close button
            Dim lCloseButton As New Button()
            lCloseButton.Relief = ReliefStyle.None
            lCloseButton.Add(Image.NewFromIconName("window-close", IconSize.Menu))
            lCloseButton.TooltipText = "Close help tab"
            AddHandler lCloseButton.Clicked, Sub() CloseHelpTab(vHelpTabId)
            lBox.PackEnd(lCloseButton, False, False, 0)
            
            lBox.ShowAll()
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreateHelpTabLabel error: {ex.Message}")
            Return New Label(vTitle)
        End Try
    End Function
    
    ''' <summary>
    ''' Updates the help tab title based on the current page
    ''' </summary>
    Private Sub UpdateHelpTabTitle(vHelpTabId As String, vTitle As String)
        Try
            If Not pHelpTabs.ContainsKey(vHelpTabId) Then Return
            
            Dim lTabInfo As TabInfo = pHelpTabs(vHelpTabId)
            If TypeOf lTabInfo.TabLabel Is Box Then
                Dim lBox As Box = CType(lTabInfo.TabLabel, Box)
                
                ' Find and update the label
                For Each lChild As Widget In lBox.Children
                    If TypeOf lChild Is Label AndAlso lChild.Name = $"help_tab_label_{vHelpTabId}" Then
                        Dim lLabel As Label = CType(lChild, Label)
                        ' Truncate title if too long
                        Dim lDisplayTitle As String = If(vTitle.Length > 30, vTitle.Substring(0, 27) & "...", vTitle)
                        lLabel.Text = $"Help: {lDisplayTitle}"
                        Exit For
                    End If
                Next
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateHelpTabTitle error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Closes a specific help tab
    ''' </summary>
    Private Sub CloseHelpTab(vHelpTabId As String)
        Try
            If Not pHelpTabs.ContainsKey(vHelpTabId) Then Return
            
            Dim lTabInfo As TabInfo = pHelpTabs(vHelpTabId)
            
            ' Find and remove the page from notebook
            For i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is lTabInfo.EditorContainer Then
                    pNotebook.RemovePage(i)
                    Exit For
                End If
            Next
            
            ' Remove from dictionary
            pHelpTabs.Remove(vHelpTabId)
            
            ' Dispose
            lTabInfo.Dispose()
            
            ' Show welcome if no tabs left
            If pNotebook.NPages = 0 Then
                ShowWelcomeTab()
            End If
            
            UpdateToolbarButtons()
            Console.WriteLine($"Closed help tab: {vHelpTabId}")
            
        Catch ex As Exception
            Console.WriteLine($"CloseHelpTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Switches to an existing help tab
    ''' </summary>
    Private Sub SwitchToHelpTab(vHelpTabId As String)
        Try
            If Not pHelpTabs.ContainsKey(vHelpTabId) Then Return
            
            Dim lTabInfo As TabInfo = pHelpTabs(vHelpTabId)
            
            ' Find the page index
            For i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is lTabInfo.EditorContainer Then
                    pNotebook.CurrentPage = i
                    Exit For
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"SwitchToHelpTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Gets the first help tab if any exist
    ''' </summary>
    Private Function GetFirstHelpTab() As String
        Try
            If pHelpTabs.Count > 0 Then
                Return pHelpTabs.Keys.First()
            End If
            Return Nothing
        Catch ex As Exception
            Console.WriteLine($"GetFirstHelpTab error: {ex.Message}")
            Return Nothing
        End Try
    End Function
    
    ''' <summary>
    ''' Checks if the current tab is a help tab
    ''' </summary>
    Private Function IsCurrentTabHelp() As Boolean
        Try
            If pNotebook Is Nothing OrElse pNotebook.CurrentPage < 0 Then Return False
            
            Dim lCurrentPage As Widget = pNotebook.GetNthPage(pNotebook.CurrentPage)
            
            For Each lKvp In pHelpTabs
                If lKvp.Value.EditorContainer Is lCurrentPage Then
                    Return True
                End If
            Next
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"IsCurrentTabHelp error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' ===== Event Handlers =====
    
    ''' <summary>
    ''' Handles help browser navigation completion
    ''' </summary>
    Private Sub OnHelpNavigationCompleted(vHelpTabId As String, vUrl As String)
        Try
            ' Get the help browser to extract title
            If pHelpTabs.ContainsKey(vHelpTabId) Then
                Dim lHelpBrowser As HelpBrowser = TryCast(pHelpTabs(vHelpTabId).EditorContainer, HelpBrowser)
                If lHelpBrowser IsNot Nothing AndAlso lHelpBrowser.WebView IsNot Nothing Then
                    Dim lTitle As String = lHelpBrowser.WebView.Title
                    If Not String.IsNullOrEmpty(lTitle) Then
                        UpdateHelpTabTitle(vHelpTabId, lTitle)
                    End If
                End If
            End If
            
            UpdateStatusBar($"Navigation completed: {vUrl}")
            
        Catch ex As Exception
            Console.WriteLine($"OnHelpNavigationCompleted error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles help browser loading state changes
    ''' </summary>
    Private Sub OnHelpLoadingStateChanged(vHelpTabId As String, vIsLoading As Boolean)
        Try
            If vIsLoading Then
                UpdateStatusBar("Loading help content...")
            Else
                UpdateStatusBar("Help content loaded")
            End If
        Catch ex As Exception
            Console.WriteLine($"OnHelpLoadingStateChanged error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Modified Help Menu Handlers =====
    
    ''' <summary>
    ''' Shows general help in a new tab
    ''' </summary>
    Public Sub ShowHelpInTab()
        Try
            OpenHelpTab()
        Catch ex As Exception
            Console.WriteLine($"ShowHelpInTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Shows context-sensitive help in a tab
    ''' </summary>
    Public Sub ShowContextHelpInTab(vContext As String)
        Try
            ' Check if we already have a help tab open
            Dim lExistingTabId As String = GetFirstHelpTab()
            
            If Not String.IsNullOrEmpty(lExistingTabId) Then
                ' Navigate existing tab to the context
                Dim lHelpBrowser As HelpBrowser = TryCast(pHelpTabs(lExistingTabId).EditorContainer, HelpBrowser)
                If lHelpBrowser IsNot Nothing Then
                    lHelpBrowser.NavigateToTopic(vContext)
                    SwitchToHelpTab(lExistingTabId)
                End If
            Else
                ' Open new help tab with context
                OpenHelpTab(vContext)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ShowContextHelpInTab error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Navigates back in the current help tab's history
    ''' </summary>
    Public Sub NavigateHelpBack()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.IsSpecialTab Then
                Dim lHelpBrowser As HelpBrowser = TryCast(lCurrentTab.EditorContainer, HelpBrowser)
                If lHelpBrowser IsNot Nothing AndAlso lHelpBrowser.WebView.CanGoBack Then
                    lHelpBrowser.WebView.GoBack()
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"NavigateHelpBack error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Navigates forward in the current help tab's history
    ''' </summary>
    Public Sub NavigateHelpForward()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.IsSpecialTab Then
                Dim lHelpBrowser As HelpBrowser = TryCast(lCurrentTab.EditorContainer, HelpBrowser)
                If lHelpBrowser IsNot Nothing AndAlso lHelpBrowser.WebView.CanGoForward Then
                    lHelpBrowser.WebView.GoForward()
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"NavigateHelpForward error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Navigates to home in the current help tab
    ''' </summary>
    Public Sub NavigateHelpHome()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.IsSpecialTab Then
                Dim lHelpBrowser As HelpBrowser = TryCast(lCurrentTab.EditorContainer, HelpBrowser)
                If lHelpBrowser IsNot Nothing Then
                    lHelpBrowser.NavigateToHome()
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"NavigateHelpHome error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Refreshes the current help tab
    ''' </summary>
    Public Sub RefreshHelp()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.IsSpecialTab Then
                Dim lHelpBrowser As HelpBrowser = TryCast(lCurrentTab.EditorContainer, HelpBrowser)
                If lHelpBrowser IsNot Nothing AndAlso lHelpBrowser.WebView IsNot Nothing Then
                    lHelpBrowser.WebView.Reload()
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"RefreshHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Opens the current help URL in an external browser
    ''' </summary>
    Public Sub OpenHelpInExternalBrowser()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.IsSpecialTab Then
                Dim lHelpBrowser As HelpBrowser = TryCast(lCurrentTab.EditorContainer, HelpBrowser)
                If lHelpBrowser IsNot Nothing AndAlso Not String.IsNullOrEmpty(lHelpBrowser.CurrentUrl) Then
                    System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo With {
                        .FileName = lHelpBrowser.CurrentUrl,
                        .UseShellExecute = True
                    })
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"OpenHelpInExternalBrowser error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Opens VB.NET documentation in a help tab
    ''' </summary>
    Public Sub ShowVBNetHelp()
        Try
            OpenHelpTab("VB.NET Language Reference", "https://learn.microsoft.com/en-us/dotnet/visual-basic/")
        Catch ex As Exception
            Console.WriteLine($"ShowVBNetHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Opens GTK# documentation in a help tab
    ''' </summary>
    Public Sub ShowGtkSharpHelp()
        Try
            OpenHelpTab("GTK# Documentation", "https://www.mono-project.com/docs/gui/gtksharp/")
        Catch ex As Exception
            Console.WriteLine($"ShowGtkSharpHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Opens .NET API documentation in a help tab
    ''' </summary>
    Public Sub ShowDotNetApiHelp()
        Try
            OpenHelpTab(".NET API Browser", "https://learn.microsoft.com/en-us/dotnet/api/?view=net-8.0")
        Catch ex As Exception
            Console.WriteLine($"ShowDotNetApiHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Opens help for a specific VB.NET keyword or API
    ''' </summary>
    ''' <param name="vKeyword">The keyword or API to look up</param>
    Public Sub ShowKeywordHelp(vKeyword As String)
        Try
            If String.IsNullOrEmpty(vKeyword) Then Return
            
            ' Construct search URL for Microsoft Learn
            Dim lSearchUrl As String = $"https://learn.microsoft.com/en-us/search/?terms={Uri.EscapeDataString(vKeyword)}&category=documentation"
            
            ' Open in help tab with keyword as title
            OpenHelpTab(vKeyword, lSearchUrl)
            
        Catch ex As Exception
            Console.WriteLine($"ShowKeywordHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Shows help for the word at the current cursor position
    ''' </summary>
    Public Sub ShowHelpForCurrentWord()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then
                ' No editor, show general help
                OpenHelpTab()
                Return
            End If
            
            ' Get word at cursor
            Dim lWord As String = GetCurrentWordAtCursor(lEditor)
            
            If Not String.IsNullOrEmpty(lWord) Then
                ShowKeywordHelp(lWord)
            Else
                ' No word at cursor, show general help
                OpenHelpTab()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ShowHelpForCurrentWord error: {ex.Message}")
        End Try
    End Sub
    
End Class 
