' MainWindow.Help.vb - Help system integration for MainWindow
Imports Gtk
Imports System
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
            OpenUrl("https://learn.microsoft.com/en-us/dotnet/Visual-basic/")
            
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
    ''' Shows keyboard shortcuts in a HelpBrowser tab with formatted HTML
    ''' </summary>
    Private Sub ShowKeyboardShortcutsDialog()
        Try
            ' Generate HTML content for keyboard shortcuts
            Dim lHtmlContent As String = BuildKeyboardShortcutsHtml()
            
            ' Open in help browser tab with the HTML content
            OpenHelpTabWithHtml("Keyboard Shortcuts", lHtmlContent)
            
        Catch ex As Exception
            Console.WriteLine($"ShowKeyboardShortcutsDialog error: {ex.Message}")
            ShowError("Error", "Failed to show keyboard shortcuts")
        End Try
    End Sub
    
    ' Replace: SimpleIDE.MainWindow.OpenHelpTabWithHtml
    ''' <summary>
    ''' Opens a help browser tab with custom HTML content and theme support
    ''' </summary>
    ''' <param name="vTitle">Title for the tab</param>
    ''' <param name="vHtmlContent">HTML content to display</param>
    Private Sub OpenHelpTabWithHtml(vTitle As String, vHtmlContent As String)
        Try
            ' Close welcome tab if it exists
            CloseWelcomeTab()
            
            ' Generate unique ID for this help tab
            Dim lHelpTabId As String = $"help_shortcuts"
            
            ' Check if shortcuts tab already exists
            For Each lKvp In pHelpTabs
                If lKvp.Key = lHelpTabId Then
                    ' Update existing tab with potentially new content
                    Dim lExistingBrowser As HelpBrowser = TryCast(pHelpTabs(lHelpTabId).EditorContainer, HelpBrowser)
                    If lExistingBrowser IsNot Nothing Then
                        lExistingBrowser.LoadHtml(vHtmlContent, "about:shortcuts")
                    End If
                    ' Switch to existing tab
                    SwitchToHelpTab(lHelpTabId)
                    Return
                End If
            Next
            
            ' Create new help browser widget with theme support
            Dim lHelpBrowser As New HelpBrowser(pSettingsManager)
            
            
            ' Load the HTML content directly
            lHelpBrowser.LoadHtml(vHtmlContent, "about:shortcuts")
            
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
            Dim lPageIndex As Integer = pNotebook.AppendPage(lHelpBrowser, vTitle)
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Store in dictionary
            pHelpTabs(lHelpTabId) = lTabInfo
            
            ' Update UI
            UpdateStatusBar($"Viewing: {vTitle}")
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"OpenHelpTabWithHtml error: {ex.Message}")
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
    
    ' Add: SimpleIDE.MainWindow.BuildKeyboardShortcutsHtml
    ' To: MainWindow.Help.vb
    ''' <summary>
    ''' Builds HTML content for keyboard shortcuts display
    ''' </summary>
    ''' <returns>HTML string with formatted keyboard shortcuts</returns>
    Private Function BuildKeyboardShortcutsHtml() As String
        Try
            Dim lHtml As New System.Text.StringBuilder()
            
            ' Start HTML document
            lHtml.AppendLine("<!DOCTYPE html>")
            lHtml.AppendLine("<html>")
            lHtml.AppendLine("<head>")
            lHtml.AppendLine("<meta charset='UTF-8'>")
            lHtml.AppendLine("<title>SimpleIDE Keyboard Shortcuts</title>")
            lHtml.AppendLine("<style>")
            
            ' CSS styling
            lHtml.AppendLine("body {")
            lHtml.AppendLine("  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;")
            lHtml.AppendLine("  line-height: 1.6;")
            lHtml.AppendLine("  color: #333;")
            lHtml.AppendLine("  max-width: 900px;")
            lHtml.AppendLine("  margin: 0 auto;")
            lHtml.AppendLine("  padding: 20px;")
            lHtml.AppendLine("  background: #f5f5f5;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine("h1 {")
            lHtml.AppendLine("  color: #2c3e50;")
            lHtml.AppendLine("  border-bottom: 3px solid #3498db;")
            lHtml.AppendLine("  padding-bottom: 10px;")
            lHtml.AppendLine("  margin-bottom: 30px;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine("h2 {")
            lHtml.AppendLine("  color: #34495e;")
            lHtml.AppendLine("  margin-top: 30px;")
            lHtml.AppendLine("  margin-bottom: 15px;")
            lHtml.AppendLine("  padding: 5px 10px;")
            lHtml.AppendLine("  background: #ecf0f1;")
            lHtml.AppendLine("  border-left: 4px solid #3498db;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine("table {")
            lHtml.AppendLine("  width: 100%;")
            lHtml.AppendLine("  border-collapse: collapse;")
            lHtml.AppendLine("  margin-bottom: 20px;")
            lHtml.AppendLine("  background: white;")
            lHtml.AppendLine("  box-shadow: 0 2px 4px rgba(0,0,0,0.1);")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine("th {")
            lHtml.AppendLine("  background: #3498db;")
            lHtml.AppendLine("  color: white;")
            lHtml.AppendLine("  text-align: left;")
            lHtml.AppendLine("  padding: 12px;")
            lHtml.AppendLine("  font-weight: 600;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine("td {")
            lHtml.AppendLine("  padding: 10px 12px;")
            lHtml.AppendLine("  border-bottom: 1px solid #ecf0f1;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine("tr:hover {")
            lHtml.AppendLine("  background: #f8f9fa;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine(".shortcut {")
            lHtml.AppendLine("  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;")
            lHtml.AppendLine("  background: #2c3e50;")
            lHtml.AppendLine("  color: #ecf0f1;")
            lHtml.AppendLine("  padding: 3px 8px;")
            lHtml.AppendLine("  border-radius: 4px;")
            lHtml.AppendLine("  font-size: 0.9em;")
            lHtml.AppendLine("  white-space: nowrap;")
            lHtml.AppendLine("  display: inline-block;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine(".description {")
            lHtml.AppendLine("  color: #555;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine(".note {")
            lHtml.AppendLine("  background: #fff3cd;")
            lHtml.AppendLine("  border: 1px solid #ffc107;")
            lHtml.AppendLine("  border-radius: 4px;")
            lHtml.AppendLine("  padding: 10px;")
            lHtml.AppendLine("  margin: 20px 0;")
            lHtml.AppendLine("}")
            
            lHtml.AppendLine("</style>")
            lHtml.AppendLine("</head>")
            lHtml.AppendLine("<body>")
            
            ' Header
            lHtml.AppendLine("<h1>🎹 SimpleIDE Keyboard Shortcuts</h1>")
            
            ' File Operations
            lHtml.AppendLine("<h2>📁 File Operations</h2>")
            lHtml.AppendLine("<table>")
            lHtml.AppendLine("<tr><th width='30%'>Shortcut</th><th>Action</th></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+N</span></td><td class='description'>New project</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+O</span></td><td class='description'>Open project</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+S</span></td><td class='description'>Save current file</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Shift+S</span></td><td class='description'>Save all files</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+W</span></td><td class='description'>Close current tab</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Q</span></td><td class='description'>Quit application</td></tr>")
            lHtml.AppendLine("</table>")
            
            ' Edit Operations
            lHtml.AppendLine("<h2>✏️ Edit Operations</h2>")
            lHtml.AppendLine("<table>")
            lHtml.AppendLine("<tr><th width='30%'>Shortcut</th><th>Action</th></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Z</span></td><td class='description'>Undo last action</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+R</span></td><td class='description'>Redo last undone action</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Shift+Z</span></td><td class='description'>Redo (alternative)</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+X</span></td><td class='description'>Cut selected text</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+C</span></td><td class='description'>Copy selected text</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+V</span></td><td class='description'>Paste from clipboard</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Shift+V</span></td><td class='description'>Smart Paste (strips comments, fixes indentation)</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+A</span></td><td class='description'>Select all text</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Y</span></td><td class='description'>Cut entire line (VB.NET style)</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+D</span></td><td class='description'>Duplicate current line</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+/</span></td><td class='description'>Toggle comment for line/selection</td></tr>")
            lHtml.AppendLine("</table>")
            
            ' View Operations
            lHtml.AppendLine("<h2>👁️ View Operations</h2>")
            lHtml.AppendLine("<table>")
            lHtml.AppendLine("<tr><th width='30%'>Shortcut</th><th>Action</th></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl++</span></td><td class='description'>Zoom in (increase text size)</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+=</span></td><td class='description'>Zoom in (alternative)</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+-</span></td><td class='description'>Zoom out (decrease text size)</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+0</span></td><td class='description'>Reset zoom to default</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Scroll</span></td><td class='description'>Zoom in/out with mouse wheel</td></tr>")
            lHtml.AppendLine("</table>")
            
            ' Navigation
            lHtml.AppendLine("<h2>🧭 Navigation</h2>")
            lHtml.AppendLine("<table>")
            lHtml.AppendLine("<tr><th width='30%'>Shortcut</th><th>Action</th></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+F</span></td><td class='description'>Find text</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+H</span></td><td class='description'>Find and replace</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+G</span></td><td class='description'>Go to line number</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>F3</span></td><td class='description'>Find next occurrence</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Shift+F3</span></td><td class='description'>Find previous occurrence</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>F2</span></td><td class='description'>Quick find (using clipboard text)</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Left</span></td><td class='description'>Move cursor to previous word</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Right</span></td><td class='description'>Move cursor to next word</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Home</span></td><td class='description'>Go to start of line</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>End</span></td><td class='description'>Go to end of line</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Home</span></td><td class='description'>Go to start of document</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+End</span></td><td class='description'>Go to end of document</td></tr>")
            lHtml.AppendLine("</table>")
            
            ' Build Operations
            lHtml.AppendLine("<h2>🔨 Build & Debug Operations</h2>")
            lHtml.AppendLine("<table>")
            lHtml.AppendLine("<tr><th width='30%'>Shortcut</th><th>Action</th></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>F5</span></td><td class='description'>Build and run project</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>F6</span></td><td class='description'>Build project only</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Shift+F6</span></td><td class='description'>Clean project</td></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>Ctrl+Shift+B</span></td><td class='description'>Build solution</td></tr>")
            lHtml.AppendLine("</table>")
            
            ' Help
            lHtml.AppendLine("<h2>❓ Help</h2>")
            lHtml.AppendLine("<table>")
            lHtml.AppendLine("<tr><th width='30%'>Shortcut</th><th>Action</th></tr>")
            lHtml.AppendLine("<tr><td><span class='shortcut'>F1</span></td><td class='description'>Context-sensitive help</td></tr>")
            lHtml.AppendLine("</table>")
            
            ' Note about additional shortcuts
            lHtml.AppendLine("<div class='note'>")
            lHtml.AppendLine("<strong>Note:</strong> Additional keyboard shortcuts may be available depending on the context. ")
            lHtml.AppendLine("Hold <span class='shortcut'>Shift</span> with navigation keys to extend selection. ")
            lHtml.AppendLine("Most standard text editing shortcuts are also supported.")
            lHtml.AppendLine("</div>")
            
            ' Footer
            lHtml.AppendLine("<hr style='margin-top: 40px; border: none; border-top: 1px solid #ddd;'>")
            lHtml.AppendLine("<p style='text-align: center; color: #999; font-size: 0.9em;'>")
            lHtml.AppendLine("SimpleIDE Keyboard Shortcuts Reference<br>")
            lHtml.AppendLine($"Generated on {DateTime.Now:yyyy-MM-dd}")
            lHtml.AppendLine("</p>")
            
            lHtml.AppendLine("</body>")
            lHtml.AppendLine("</html>")
            
            Return lHtml.ToString()
            
        Catch ex As Exception
            Console.WriteLine($"BuildKeyboardShortcutsHtml error: {ex.Message}")
            ' Return a simple error page
            Return "<html><body><h1>Error</h1><p>Failed to generate keyboard shortcuts.</p></body></html>"
        End Try
    End Function    
    
End Class
