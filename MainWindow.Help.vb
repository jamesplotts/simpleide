' MainWindow.Help.vb - Help system integration for MainWindow
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Partial Public Class MainWindow

    ' Help system integration

    
    ' ===== Help Menu Handlers =====
    
    ' Show help contents
    Public Sub OnHelpContents(vSender As Object, vArgs As EventArgs)
        Try
            ShowHelpPanel()
            
        Catch ex As Exception
            Console.WriteLine($"OnHelpContents error: {ex.Message}")
        End Try
    End Sub
    
    ' Show VB.NET reference
    Public Sub OnVBReference(vSender As Object, vArgs As EventArgs)
        Try
            ShowHelpPanel()
            ' TODO: Navigate to VB.NET reference in help panel
            
        Catch ex As Exception
            Console.WriteLine($"OnVBReference error: {ex.Message}")
        End Try
    End Sub
    
    ' Show GTK# reference
    Public Sub OnGTKReference(vSender As Object, vArgs As EventArgs)
        Try
            ShowHelpPanel()
            ' TODO: Navigate to GTK# reference in help panel
            
        Catch ex As Exception
            Console.WriteLine($"OnGTKReference error: {ex.Message}")
        End Try
    End Sub
    
    ' Open online documentation
    Public Sub OnOnlineDocumentation(vSender As Object, vArgs As EventArgs)
        Try
            OpenUrl("https://learn.microsoft.com/en-us/dotnet/visual-basic/")
            
        Catch ex As Exception
            Console.WriteLine($"OnOnlineDocumentation error: {ex.Message}")
        End Try
    End Sub
    
    ' Context-sensitive help (F1)

    ''' <summary>
    ''' Shows context-sensitive help based on current editor context
    ''' </summary>
    Public Sub OnContextHelp(vSender As Object, vArgs As EventArgs)
        Try
            ' Get current context
            Dim lContext As String = GetCurrentHelpContext()
            
            If Not String.IsNullOrEmpty(lContext) Then
                ShowContextHelpInTab(lContext)
            Else
                ' No specific context, show general help
                OpenHelpTab()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnContextHelp error: {ex.Message}")
        End Try
    End Sub 
   
    ' Show keyboard shortcuts
    Public Sub OnKeyboardShortcuts(vSender As Object, vArgs As EventArgs)
        Try
            ShowKeyboardShortcutsDialog()
            
        Catch ex As Exception
            Console.WriteLine($"OnKeyboardShortcuts error: {ex.Message}")
        End Try
    End Sub
    
    
    ' ===== Help System Implementation =====
    
    ''' <summary>
    ''' Shows the help browser in a center tab (replaced bottom panel approach)
    ''' </summary>
    Private Sub ShowHelpPanel()
        Try
            ' Open help in a new tab instead of bottom panel
            OpenHelpTab()
        Catch ex As Exception
            Console.WriteLine($"ShowHelpPanel error: {ex.Message}")
        End Try
    End Sub    

    ' Get current help context based on active editor
    Private Function GetCurrentHelpContext() As String
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab?.Editor Is Nothing Then Return ""
            
            ' Get current word under cursor
            Dim lCurrentWord As String = GetCurrentWordAtCursor(lCurrentTab.Editor)
            
            If Not String.IsNullOrEmpty(lCurrentWord) Then
                ' Return the word directly for context help
                Return lCurrentWord
            End If
            
            Return ""
            
        Catch ex As Exception
            Console.WriteLine($"GetCurrentHelpContext error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    ' Get the current word at cursor position
    Private Function GetCurrentWordAtCursor(vEditor As IEditor) As String
        Try
            If vEditor Is Nothing Then Return ""
            
            Dim lPosition As EditorPosition = vEditor.GetCursorPosition()
            Dim lLine As String = vEditor.GetLineText(lPosition.Line)
            
            If String.IsNullOrEmpty(lLine) OrElse lPosition.Column >= lLine.Length Then
                Return ""
            End If
            
            ' Find word boundaries
            Dim lStart As Integer = lPosition.Column
            Dim lEnd As Integer = lPosition.Column
            
            ' Move start backwards to find word start
            While lStart > 0 AndAlso Char.IsLetterOrDigit(lLine(lStart - 1))
                lStart -= 1
            End While
            
            ' Move end forwards to find word end
            While lEnd < lLine.Length AndAlso Char.IsLetterOrDigit(lLine(lEnd))
                lEnd += 1
            End While
            
            If lEnd > lStart Then
                Return lLine.Substring(lStart, lEnd - lStart)
            End If
            
            Return ""
            
        Catch ex As Exception
            Console.WriteLine($"GetCurrentWordAtCursor error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    ' Show context-sensitive help for a specific topic
    Private Sub ShowContextHelp(vContext As String)
        Try
            ShowHelpPanel()
            
            ' Navigate to specific help topic using available methods
            If pHelpViewerPanel IsNot Nothing Then
                ' Use available methods like ShowVBNetHelp, ShowGtkHelp, etc.
                Select Case vContext.ToLower()
                    Case "string", "integer", "boolean", "double", "date"
                        pHelpViewerPanel.ShowVBNetHelp()
                    Case "button", "label", "textbox", "window"
                        pHelpViewerPanel.ShowGtkHelp()
                    Case Else
                        pHelpViewerPanel.ShowDotNetHelp()
                End Select
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ShowContextHelp error: {ex.Message}")
        End Try
    End Sub
    
    ' Show keyboard shortcuts dialog
    ''' <summary>
    ''' Shows keyboard shortcuts in a HelpBrowser tab
    ''' </summary>
    Private Sub ShowKeyboardShortcutsDialog()
        Try
            Dim lSections As List(Of HelpSection) = BuildKeyboardShortcutsSections()

            ' Open in help browser tab
            OpenHelpTabWithSections("Keyboard Shortcuts", lSections)

        Catch ex As Exception
            Console.WriteLine($"ShowKeyboardShortcutsDialog error: {ex.Message}")
            ShowError("Error", "Failed to show keyboard shortcuts")
        End Try
    End Sub

    ' Replace: SimpleIDE.MainWindow.OpenHelpTabWithSections
    ''' <summary>
    ''' Opens a help browser tab with custom sectioned content and theme support
    ''' </summary>
    ''' <param name="vTitle">Title for the tab</param>
    ''' <param name="vSections">Sections to display</param>
    Private Sub OpenHelpTabWithSections(vTitle As String, vSections As List(Of HelpSection))
        Try
            ' Generate unique ID for this help tab
            Dim lHelpTabId As String = $"help_shortcuts"
            Dim lPageUrl As String = "simpleide://shortcuts"

            ' Check if shortcuts tab already exists
            For Each lKvp In pHelpTabs
                If lKvp.Key = lHelpTabId Then
                    ' Update existing tab with potentially new content
                    Dim lExistingBrowser As HelpBrowser = TryCast(pHelpTabs(lHelpTabId).EditorContainer, HelpBrowser)
                    If lExistingBrowser IsNot Nothing Then
                        lExistingBrowser.ShowSections(vTitle, vSections, lPageUrl)
                    End If
                    ' Switch to existing tab
                    SwitchToHelpTab(lHelpTabId)
                    Return
                End If
            Next

            ' Create new help browser widget with theme support
            Dim lHelpBrowser As New HelpBrowser(pSettingsManager)
            lHelpBrowser.SetThemeManager(pThemeManager)

            ' Show the sectioned content directly
            lHelpBrowser.ShowSections(vTitle, vSections, lPageUrl)

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
            lTabInfo.TabLabel = CreateHelpTabLabel(lHelpTabId, vTitle)

            ' Add to notebook
            pNotebook.AppendPage(lHelpBrowser, vTitle)
            pNotebook.ShowAll()

            ' Close the welcome tab (if any) now that the help tab is already in place -
            ' closing it first would transiently drop the notebook to 0 pages, which
            ' OnCustomNotebookTabClosed treats as "all tabs closed" and reacts to by
            ' clearing the Object Explorer's entire tree/expanded-node state
            CloseWelcomeTab()

            pNotebook.CurrentPage = pNotebook.NPages - 1

            ' Store in dictionary
            pHelpTabs(lHelpTabId) = lTabInfo

            ' Update UI
            UpdateStatusBar($"Viewing: {vTitle}")
            UpdateToolbarButtons()

        Catch ex As Exception
            Console.WriteLine($"OpenHelpTabWithSections error: {ex.Message}")
            ShowError("Error", $"Failed to open help tab: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Builds keyboard shortcuts text with correct VB.NET conventions
    ''' </summary>
    Private Function BuildKeyboardShortcutsText() As String
        Try
            Dim lText As New System.Text.StringBuilder()
            
            lText.AppendLine("KEYBOARD SHORTCUTS")
            lText.AppendLine("==================")
            lText.AppendLine()
            
            lText.AppendLine("File Operations:")
            lText.AppendLine("  Ctrl+N          New project")
            lText.AppendLine("  Ctrl+O          Open project")
            lText.AppendLine("  Ctrl+S          Save")
            lText.AppendLine("  Ctrl+Shift+S    Save All")
            lText.AppendLine("  Ctrl+W          Close Tab")
            lText.AppendLine("  Ctrl+Q          Quit")
            lText.AppendLine()
            
            lText.AppendLine("Edit Operations:")
            lText.AppendLine("  Ctrl+Z          Undo")
            lText.AppendLine("  Ctrl+R          Redo")
            lText.AppendLine("  Ctrl+Shift+Z    Redo (alternative)")
            lText.AppendLine("  Ctrl+Y          Cut Line (VB classic)")
            lText.AppendLine("  Ctrl+X          Cut selection")
            lText.AppendLine("  Ctrl+C          Copy")
            lText.AppendLine("  Ctrl+V          Paste")
            lText.AppendLine("  Ctrl+Shift+V    Smart Paste (strips comments, fixes indentation)")
            lText.AppendLine("  Ctrl+A          Select All")
            lText.AppendLine("  Ctrl+/          Toggle Comment")
            lText.AppendLine()      
      
            lText.AppendLine("Navigation:")
            lText.AppendLine("  Ctrl+F          Find")
            lText.AppendLine("  Ctrl+H          Replace")
            lText.AppendLine("  Ctrl+G          Go to Line")
            lText.AppendLine("  F3              Find Next")
            lText.AppendLine("  Shift+F3        Find Previous")
            lText.AppendLine()
            
            lText.AppendLine("Code Operations:")
            lText.AppendLine("  F5              Run/Debug")
            lText.AppendLine("  Ctrl+F5         Run without debugging")
            lText.AppendLine("  Shift+F5        Stop debugging")
            lText.AppendLine("  F6              Build Project")
            lText.AppendLine("  Ctrl+Shift+B    Build Solution")
            lText.AppendLine("  F12             Go to Definition")
            lText.AppendLine()
            
            lText.AppendLine("View Operations:")
            lText.AppendLine("  Ctrl+E          Toggle Project Explorer")
            lText.AppendLine("  F11             Toggle Full Screen")
            lText.AppendLine("  Ctrl+Tab        Next Tab")
            lText.AppendLine("  Ctrl+Shift+Tab  Previous Tab")
            lText.AppendLine()
            
            lText.AppendLine("Text Navigation:")
            lText.AppendLine("  Ctrl+Home       Go to start of document")
            lText.AppendLine("  Ctrl+End        Go to end of document")
            lText.AppendLine("  Ctrl+Left       Previous word")
            lText.AppendLine("  Ctrl+Right      Next word")
            lText.AppendLine("  Home            Start of line")
            lText.AppendLine("  End             End of line")
            lText.AppendLine("  Page Up         Page up")
            lText.AppendLine("  Page Down       Page down")
            lText.AppendLine()
            
            lText.AppendLine("Special Keys:")
            lText.AppendLine("  Tab             Indent/Accept IntelliSense")
            lText.AppendLine("  Shift+Tab       Outdent")
            lText.AppendLine("  Escape          Cancel operation/Clear selection")
            lText.AppendLine("  Ctrl+Space      Trigger IntelliSense")
            lText.AppendLine("  Ctrl+Shift+Space  Parameter hints")
            lText.AppendLine()
            
            lText.AppendLine("View Operations:")
            lText.AppendLine("  Ctrl++          Zoom In (also Ctrl+=)")
            lText.AppendLine("  Ctrl+-          Zoom Out")
            lText.AppendLine("  Ctrl+0          Reset Zoom")
            lText.AppendLine("  Ctrl+Scroll     Zoom In/Out (mouse wheel)")
            lText.AppendLine()
            
            lText.AppendLine("Note: Ctrl+Y is the traditional VB 'Cut Line' command,")
            lText.AppendLine("      Not Redo. Use Ctrl+R Or Ctrl+Shift+Z for Redo.")
            
            Return lText.ToString()
            
        Catch ex As Exception
            Console.WriteLine($"BuildKeyboardShortcutsText error: {ex.Message}")
            Return "error building keyboard shortcuts text"
        End Try
    End Function
    
    
    ' Open URL in default browser
    Private Sub OpenUrl(vUrl As String)
        Try
            If String.IsNullOrEmpty(vUrl) Then Return
            
            ' Try to open URL using xdg-open (Linux standard)
            Dim lProcess As New Process()
            lProcess.StartInfo.FileName = "xdg-open"
            lProcess.StartInfo.Arguments = vUrl
            lProcess.StartInfo.UseShellExecute = False
            lProcess.StartInfo.RedirectStandardOutput = True
            lProcess.StartInfo.RedirectStandardError = True
            lProcess.Start()
            
        Catch ex As Exception
            Console.WriteLine($"OpenUrl error: {ex.Message}")
            
            ' Fallback: show message dialog with URL
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.Info,
                ButtonsType.Ok,
                $"Please open the following Url in your web browser:{Environment.NewLine}{Environment.NewLine}{vUrl}"
            )
            lDialog.Run()
            lDialog.Destroy()
        End Try
    End Sub
    
    ' ===== Help Panel Event Handlers =====
    
    Private Sub OnHelpTitleChanged(vTitle As String)
        Try
            ' Update help tab title if needed
            If pHelpViewerPanel IsNot Nothing AndAlso pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.SetTabLabelText(pHelpViewerPanel, $"Help - {vTitle}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnHelpTitleChanged error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnHelpNavigationChanged(vCanGoBack As Boolean, vCanGoForward As Boolean)
        Try
            ' Update help navigation buttons if they exist
            ' This would be implemented if we had navigation buttons in the help panel
            
        Catch ex As Exception
            Console.WriteLine($"OnHelpNavigationChanged error: {ex.Message}")
        End Try
    End Sub
    
    ' Add: SimpleIDE.MainWindow.BuildKeyboardShortcutsSections
    ' To: MainWindow.Help.vb
    ''' <summary>
    ''' Builds the sectioned keyboard shortcuts content for display in a HelpBrowser tab
    ''' </summary>
    ''' <returns>The keyboard shortcuts grouped into sections</returns>
    Private Function BuildKeyboardShortcutsSections() As List(Of HelpSection)
        Try
            Dim lSections As New List(Of HelpSection)

            Dim lFileOps As New HelpSection("📁 File Operations")
            lFileOps.Items.Add(New HelpResourceItem("Ctrl+N", "New project"))
            lFileOps.Items.Add(New HelpResourceItem("Ctrl+O", "Open project"))
            lFileOps.Items.Add(New HelpResourceItem("Ctrl+S", "Save current file"))
            lFileOps.Items.Add(New HelpResourceItem("Ctrl+Shift+S", "Save all files"))
            lFileOps.Items.Add(New HelpResourceItem("Ctrl+W", "Close current tab"))
            lFileOps.Items.Add(New HelpResourceItem("Ctrl+Q", "Quit application"))
            lSections.Add(lFileOps)

            Dim lEditOps As New HelpSection("✏️ Edit Operations")
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+Z", "Undo last action"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+R", "Redo last undone action"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+Shift+Z", "Redo (alternative)"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+X", "Cut selected text"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+C", "Copy selected text"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+V", "Paste from clipboard"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+Shift+V", "Smart Paste (strips comments, fixes indentation)"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+A", "Select all text"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+Y", "Cut entire line (VB.NET style)"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+D", "Duplicate current line"))
            lEditOps.Items.Add(New HelpResourceItem("Ctrl+/", "Toggle comment for line/selection"))
            lSections.Add(lEditOps)

            Dim lViewOps As New HelpSection("👁️ View Operations")
            lViewOps.Items.Add(New HelpResourceItem("Ctrl++", "Zoom in (increase text size)"))
            lViewOps.Items.Add(New HelpResourceItem("Ctrl+=", "Zoom in (alternative)"))
            lViewOps.Items.Add(New HelpResourceItem("Ctrl+-", "Zoom out (decrease text size)"))
            lViewOps.Items.Add(New HelpResourceItem("Ctrl+0", "Reset zoom to default"))
            lViewOps.Items.Add(New HelpResourceItem("Ctrl+Scroll", "Zoom in/out with mouse wheel"))
            lSections.Add(lViewOps)

            Dim lNavigation As New HelpSection("🧭 Navigation")
            lNavigation.Items.Add(New HelpResourceItem("Ctrl+F", "Find text"))
            lNavigation.Items.Add(New HelpResourceItem("Ctrl+H", "Find and replace"))
            lNavigation.Items.Add(New HelpResourceItem("Ctrl+G", "Go to line number"))
            lNavigation.Items.Add(New HelpResourceItem("F3", "Find next occurrence"))
            lNavigation.Items.Add(New HelpResourceItem("Shift+F3", "Find previous occurrence"))
            lNavigation.Items.Add(New HelpResourceItem("F2", "Quick find (using clipboard text)"))
            lNavigation.Items.Add(New HelpResourceItem("Ctrl+Left", "Move cursor to previous word"))
            lNavigation.Items.Add(New HelpResourceItem("Ctrl+Right", "Move cursor to next word"))
            lNavigation.Items.Add(New HelpResourceItem("Home", "Go to start of line"))
            lNavigation.Items.Add(New HelpResourceItem("End", "Go to end of line"))
            lNavigation.Items.Add(New HelpResourceItem("Ctrl+Home", "Go to start of document"))
            lNavigation.Items.Add(New HelpResourceItem("Ctrl+End", "Go to end of document"))
            lSections.Add(lNavigation)

            Dim lBuildOps As New HelpSection("🔨 Build & Debug Operations")
            lBuildOps.Items.Add(New HelpResourceItem("F5", "Build and run project"))
            lBuildOps.Items.Add(New HelpResourceItem("F6", "Build project only"))
            lBuildOps.Items.Add(New HelpResourceItem("Shift+F6", "Clean project"))
            lBuildOps.Items.Add(New HelpResourceItem("Ctrl+Shift+B", "Build solution"))
            lSections.Add(lBuildOps)

            Dim lHelpOps As New HelpSection("❓ Help")
            lHelpOps.Items.Add(New HelpResourceItem("F1", "Context-sensitive help"))
            lSections.Add(lHelpOps)

            Dim lNote As New HelpSection("Note")
            lNote.Items.Add(New HelpResourceItem("", "Additional keyboard shortcuts may be available depending on the context. Hold Shift with navigation keys to extend selection. Most standard text editing shortcuts are also supported."))
            lSections.Add(lNote)

            Return lSections

        Catch ex As Exception
            Console.WriteLine($"BuildKeyboardShortcutsSections error: {ex.Message}")
            Dim lErrorSections As New List(Of HelpSection)
            Dim lErrorSection As New HelpSection("Error")
            lErrorSection.Items.Add(New HelpResourceItem("", "Failed to generate keyboard shortcuts."))
            lErrorSections.Add(lErrorSection)
            Return lErrorSections
        End Try
    End Function
    
End Class
