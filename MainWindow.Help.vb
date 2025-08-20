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
    Public Sub OnContextHelp(vSender As Object, vArgs As EventArgs)
        Try
            ' Get current context
            Dim lContext As String = GetCurrentHelpContext()
            
            If Not String.IsNullOrEmpty(lContext) Then
                ShowContextHelp(lContext)
            Else
                ShowHelpPanel()
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
    
    ' Show the help panel in bottom panel
    Private Sub ShowHelpPanel()
        Try
            ' Check if help panel exists
            If pHelpViewerPanel Is Nothing Then
                
                ' Add event handlers
                AddHandler pHelpViewerPanel.TitleChanged, AddressOf OnHelpTitleChanged
                
                ' Add to bottom panel
                'p'BottomNotebook.AppendPage(pHelpViewerPanel, New Label("Help"))
            End If
            
            ' Show bottom panel
'            pBottomPanel.Position = pBottomPanel.Allocation.Height - 300
            pBottomPanelManager.ShowTabByType(BottomPanelManager.BottomPanelTab.eHelpViewer)
            
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
    Private Sub ShowKeyboardShortcutsDialog()
        Try
            Dim lDialog As New Dialog("Keyboard Shortcuts", Me, DialogFlags.Modal)
            lDialog.SetDefaultSize(600, 500)
            lDialog.BorderWidth = 10
            
            ' Create content area
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic)
            lScrolled.SetSizeRequest(580, 450)
            
            Dim lMainBox As New Box(Orientation.Vertical, 10)
            lMainBox.MarginStart = 20
            lMainBox.MarginEnd = 20
            lMainBox.MarginTop = 20
            lMainBox.MarginBottom = 20
            
            ' Add keyboard shortcuts content here
            Dim lLabel As New Label("Keyboard Shortcuts will be displayed here")
            lMainBox.PackStart(lLabel, False, False, 0)
            
            lScrolled.Add(lMainBox)
            lDialog.ContentArea.Add(lScrolled)
            
            ' FIXED: Add close button with proper signature
            lDialog.AddButton("Close", CInt(ResponseType.Close))
            
            lDialog.ShowAll()
            lDialog.Run()
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"ShowKeyboardShortcutsDialog error: {ex.Message}")
        End Try
    End Sub

    ' Build keyboard shortcuts text
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
            lText.AppendLine("  Ctrl+y          Redo")
            lText.AppendLine("  Ctrl+x          Cut")
            lText.AppendLine("  Ctrl+C          Copy")
            lText.AppendLine("  Ctrl+V          Paste")
            lText.AppendLine("  Ctrl+A          Select All")
            lText.AppendLine("  Ctrl+/          Toggle Comment")
            lText.AppendLine()
            
            lText.AppendLine("Navigation:")
            lText.AppendLine("  Ctrl+F          Find")
            lText.AppendLine("  Ctrl+H          Replace")
            lText.AppendLine("  Ctrl+g          Go to Line")
            lText.AppendLine("  F3              Find Next")
            lText.AppendLine("  Shift+F3        Find Previous")
            lText.AppendLine()
            
            lText.AppendLine("code Operations:")
            lText.AppendLine("  Tab             Indent")
            lText.AppendLine("  Shift+Tab       Outdent")
            lText.AppendLine("  Ctrl+Space      IntelliSense")
            lText.AppendLine("  Ctrl+K,C        Comment Selection")
            lText.AppendLine("  Ctrl+K,U        Uncomment Selection")
            lText.AppendLine()
            
            lText.AppendLine("Build Operations:")
            lText.AppendLine("  F5              Build Project")           ' CHANGED: Was "Build and Run"
            lText.AppendLine("  Ctrl+F5         Build and Run")          ' CHANGED: Added new shortcut
            lText.AppendLine("  F6              Rebuild Project")        ' CHANGED: Was "Build Solution"
            lText.AppendLine("  Shift+F5        Stop Debugging")         ' Future feature
            lText.AppendLine()
            
            lText.AppendLine("Debug Operations:")
            lText.AppendLine("  F9              Toggle Breakpoint")
            lText.AppendLine("  F10             Step Over")
            lText.AppendLine("  F11             Step Into")
            lText.AppendLine()
            
            lText.AppendLine("View Operations:")
            lText.AppendLine("  F4              Properties")
            lText.AppendLine("  Ctrl+\\         project Explorer")
            lText.AppendLine("  Ctrl+Shift+E    error List")
            lText.AppendLine("  Ctrl+Shift+O    output Window")
            lText.AppendLine()
            
            lText.AppendLine("Help:")
            lText.AppendLine("  F1              Context Help")
            lText.AppendLine("  Ctrl+F1         Help Contents")
            
            Return lText.ToString()
            
        Catch ex As Exception
            Console.WriteLine($"BuildKeyboardShortcutsText error: {ex.Message}")
            Return "error building shortcuts list."
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
    
End Class
