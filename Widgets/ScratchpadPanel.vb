' ScratchpadPanel.vb
' Created: 2025-08-05 20:35:45
' ScratchpadPanel.vb - Main scratchpad UI widget
Imports Gtk
Imports System
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Diagnostics
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

Namespace Widgets
    
    Public Class ScratchpadPanel
        Inherits Box
        
        ' Private fields
        Private pScratchpadManager As ScratchpadManager
        Private pCurrentScratchpad As ScratchpadData
        Private pTextView As TextView
        Private pTextBuffer As TextBuffer
        Private pScratchpadCombo As ComboBoxText
        Private pScopeLabel As Label
        Private pStatusLabel As Label
        Private pSaveTimer As UInteger
        Private pIsModified As Boolean = False
        Private pIsUpdating As Boolean = False
        
        ' Toolbar buttons
        Private pNewButton As ToolButton
        Private pDeleteButton As ToolButton
        Private pCopyButton As ToolButton
        Private pPasteButton As ToolButton
        Private pCutButton As ToolButton
        Private pClearButton As ToolButton
        Private pInsertDateButton As ToolButton
        Private pInsertTodoButton As ToolButton
        Private pSearchButton As ToolButton
        
        ' Events
        Public Event ScratchpadChanged(vScratchpad As ScratchpadData)
        Public Event CloseRequested()
        
        ' Constructor
        Public Sub New(vScratchpadManager As ScratchpadManager)
            MyBase.New(Orientation.Vertical, 0)
            
            pScratchpadManager = vScratchpadManager
            
            BuildUI()
            ConnectEvents()
            LoadScratchpads()
            
            ' Select default scratchpad
            SelectDefaultScratchpad()
        End Sub
        
        ' ===== UI Building =====
        
        Private Sub BuildUI()
            Try
                ' Create header toolbar
                Dim lHeaderBox As Widget = CreateHeaderToolbar()
                PackStart(lHeaderBox, False, False, 0)
                
                ' Create separator
                PackStart(New Separator(Orientation.Horizontal), False, False, 2)
                
                ' Create scrolled window for text view
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                ' Create text view
                pTextView = New TextView()
                pTextBuffer = pTextView.Buffer
                
                ' Configure text view
                pTextView.WrapMode = WrapMode.Word
                pTextView.LeftMargin = 10
                pTextView.RightMargin = 10
                pTextView.TopMargin = 10
                pTextView.BottomMargin = 10
                
                ' Apply font settings
                ApplyFontSettings()
                
                ' Enable hyperlink detection
                ConfigureHyperlinkDetection()
                
                lScrolled.Add(pTextView)
                PackStart(lScrolled, True, True, 0)
                
                ' Create status bar
                Dim lStatusBox As Widget = CreateStatusBar()
                PackStart(lStatusBox, False, False, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        Private Function CreateHeaderToolbar() As Widget
            Try
                Dim lBox As New Box(Orientation.Horizontal, 5)
                lBox.MarginStart = 5
                lBox.MarginEnd = 5
                lBox.MarginTop = 5
                lBox.MarginBottom = 5
                
                ' Scratchpad selector
                pScratchpadCombo = New ComboBoxText()
                pScratchpadCombo.WidthRequest = 200
                lBox.PackStart(pScratchpadCombo, False, False, 0)
                
                ' Scope label
                pScopeLabel = New Label("[Global]")
                pScopeLabel.Markup = "<b>[Global]</b>"
                lBox.PackStart(pScopeLabel, False, False, 0)
                
                ' Separator
                lBox.PackStart(New Separator(Orientation.Vertical), False, False, 5)
                
                ' New button
                pNewButton = CreateToolButton("document-New", "New Scratchpad")
                lBox.PackStart(pNewButton, False, False, 0)
                
                ' Delete button
                pDeleteButton = CreateToolButton("edit-Delete", "Delete Scratchpad")
                lBox.PackStart(pDeleteButton, False, False, 0)
                
                ' Separator
                lBox.PackStart(New Separator(Orientation.Vertical), False, False, 5)
                
                ' Copy button
                pCopyButton = CreateToolButton("edit-Copy", "Copy")
                lBox.PackStart(pCopyButton, False, False, 0)
                
                ' Cut button
                pCutButton = CreateToolButton("edit-Cut", "Cut")
                lBox.PackStart(pCutButton, False, False, 0)
                
                ' Paste button
                pPasteButton = CreateToolButton("edit-Paste", "Paste")
                lBox.PackStart(pPasteButton, False, False, 0)
                
                ' Clear button
                pClearButton = CreateToolButton("edit-Clear", "Clear All")
                lBox.PackStart(pClearButton, False, False, 0)
                
                ' Separator
                lBox.PackStart(New Separator(Orientation.Vertical), False, False, 5)
                
                ' Insert date button
                pInsertDateButton = CreateToolButton("x-office-calendar", "Insert Date/Time")
                lBox.PackStart(pInsertDateButton, False, False, 0)
                
                ' Insert TODO button
                pInsertTodoButton = CreateToolButton("emblem-important", "Insert TODO")
                lBox.PackStart(pInsertTodoButton, False, False, 0)
                
                ' Search button
                pSearchButton = CreateToolButton("edit-Find", "Search")
                lBox.PackStart(pSearchButton, False, False, 0)
                
                Return lBox
                
            Catch ex As Exception
                Console.WriteLine($"CreateHeaderToolbar error: {ex.Message}")
                Return New Box(Orientation.Horizontal, 0)
            End Try
        End Function
        
        Private Function CreateToolButton(vIconName As String, vTooltip As String) As ToolButton
            Dim lButton As New ToolButton(Nothing, Nothing)
            lButton.IconWidget = Image.NewFromIconName(vIconName, IconSize.SmallToolbar)
            lButton.TooltipText = vTooltip
            Return lButton
        End Function
        
        Private Function CreateStatusBar() As Widget
            Try
                Dim lBox As New Box(Orientation.Horizontal, 10)
                lBox.MarginStart = 10
                lBox.MarginEnd = 10
                lBox.MarginTop = 2
                lBox.MarginBottom = 2
                
                pStatusLabel = New Label("Ready")
                pStatusLabel.Xalign = 0
                lBox.PackStart(pStatusLabel, True, True, 0)
                
                Return lBox
                
            Catch ex As Exception
                Console.WriteLine($"CreateStatusBar error: {ex.Message}")
                Return New Box(Orientation.Horizontal, 0)
            End Try
        End Function
        
        ' ===== Event Handling =====
        
        Private Sub ConnectEvents()
            Try
                ' Manager events
                AddHandler pScratchpadManager.ScratchpadListChanged, AddressOf  OnScratchpadListChanged
                
                ' Combo box events
                AddHandler pScratchpadCombo.Changed, AddressOf OnScratchpadSelected
                
                ' Text buffer events
                AddHandler pTextBuffer.Changed, AddressOf OnTextChanged
                
                ' Button events
                AddHandler pNewButton.Clicked, AddressOf OnNewScratchpad
                AddHandler pDeleteButton.Clicked, AddressOf OnDeleteScratchpad
                AddHandler pCopyButton.Clicked, AddressOf OnCopy
                AddHandler pCutButton.Clicked, AddressOf OnCut
                AddHandler pPasteButton.Clicked, AddressOf OnPaste
                AddHandler pClearButton.Clicked, AddressOf OnClear
                AddHandler pInsertDateButton.Clicked, AddressOf OnInsertDate
                AddHandler pInsertTodoButton.Clicked, AddressOf OnInsertTodo
                AddHandler pSearchButton.Clicked, AddressOf OnSearch
                
                ' Text view events for hyperlinks
                AddHandler pTextView.ButtonPressEvent, AddressOf OnTextViewButtonPress
                AddHandler pTextView.MotionNotifyEvent, AddressOf OnTextViewMotionNotify
                
            Catch ex As Exception
                Console.WriteLine($"ConnectEvents error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Scratchpad Management =====
        
        Private Sub LoadScratchpads()
            Try
                pIsUpdating = True
                
                ' Clear combo box
                pScratchpadCombo.RemoveAll()
                
                ' Get all scratchpads
                Dim lScratchpads As List(Of ScratchpadData) = pScratchpadManager.GetAllScratchpads()
                
                ' Add to combo box
                For Each lScratchpad In lScratchpads
                    Dim lScopePrefix As String = If(lScratchpad.Scope = ScratchpadScope.eGlobal, "[g] ", "[P] ")
                    pScratchpadCombo.AppendText($"{lScopePrefix}{lScratchpad.Name}")
                Next
                
                pIsUpdating = False
                
            Catch ex As Exception
                Console.WriteLine($"LoadScratchpads error: {ex.Message}")
                pIsUpdating = False
            End Try
        End Sub
        
        Private Sub SelectDefaultScratchpad()
            Try
                Dim lDefault As ScratchpadData = pScratchpadManager.GetDefaultScratchpad()
                If lDefault IsNot Nothing Then
                    LoadScratchpad(lDefault)
                    
                    ' Select in combo box
                    Dim lScratchpads As List(Of ScratchpadData) = pScratchpadManager.GetAllScratchpads()
                    For i As Integer = 0 To lScratchpads.Count - 1
                        If lScratchpads(i).Id = lDefault.Id Then
                            pScratchpadCombo.Active = i
                            Exit For
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectDefaultScratchpad error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub LoadScratchpad(vScratchpad As ScratchpadData)
            Try
                pIsUpdating = True
                pCurrentScratchpad = vScratchpad
                
                If vScratchpad IsNot Nothing Then
                    ' Load content
                    pTextBuffer.Text = vScratchpad.Content
                    
                    ' Update scope label
                    Dim lScopeText As String = If(vScratchpad.Scope = ScratchpadScope.eGlobal, "[Global]", "[project]")
                    pScopeLabel.Markup = $"<b>{lScopeText}</b>"
                    
                    ' Update status
                    UpdateStatus($"loaded: {vScratchpad.Name}")
                Else
                    pTextBuffer.Text = ""
                    pScopeLabel.Markup = "<b>[None]</b>"
                End If
                
                pIsModified = False
                pIsUpdating = False
                
            Catch ex As Exception
                Console.WriteLine($"LoadScratchpad error: {ex.Message}")
                pIsUpdating = False
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        Private Sub OnScratchpadListChanged()
            LoadScratchpads()
        End Sub
        
        Private Sub OnScratchpadSelected(vSender As Object, vArgs As EventArgs)
            Try
                If pIsUpdating Then Return
                
                Dim lIndex As Integer = pScratchpadCombo.Active
                If lIndex < 0 Then Return
                
                ' Save current if modified
                SaveCurrentScratchpad()
                
                ' Load selected scratchpad
                Dim lScratchpads As List(Of ScratchpadData) = pScratchpadManager.GetAllScratchpads()
                If lIndex < lScratchpads.Count Then
                    LoadScratchpad(lScratchpads(lIndex))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnScratchpadSelected error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnTextChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pIsUpdating Then Return
                
                pIsModified = True
                
                ' Cancel existing timer
                If pSaveTimer > 0 Then
                    GLib.Source.Remove(pSaveTimer)
                End If
                
                ' Start new timer for auto-save (2.5 seconds)
                pSaveTimer = GLib.Timeout.Add(2500, AddressOf AutoSave)
                
                UpdateStatus("Modified...")
                
            Catch ex As Exception
                Console.WriteLine($"OnTextChanged error: {ex.Message}")
            End Try
        End Sub
        
        Private Function AutoSave() As Boolean
            Try
                SaveCurrentScratchpad()
                pSaveTimer = 0
                Return False ' Don't repeat
                
            Catch ex As Exception
                Console.WriteLine($"AutoSave error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Sub SaveCurrentScratchpad()
            Try
                If pCurrentScratchpad IsNot Nothing AndAlso pIsModified Then
                    pCurrentScratchpad.Content = pTextBuffer.Text
                    pScratchpadManager.UpdateScratchpad(pCurrentScratchpad)
                    pIsModified = False
                    UpdateStatus("Saved")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SaveCurrentScratchpad error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Toolbar Button Handlers =====
        
        Private Sub OnNewScratchpad(vSender As Object, vArgs As EventArgs)
            Try
                ' Show dialog to get name and scope
                Dim lDialog As New Dialog("New Scratchpad", 
                                        CType(Me.Toplevel, Window), 
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                        "Cancel", ResponseType.Cancel,
                                        "Create", ResponseType.Accept)
                
                lDialog.SetDefaultSize(400, 200)
                
                ' Create content
                Dim lVBox As New Box(Orientation.Vertical, 10)
                lVBox.MarginStart = 10
                lVBox.MarginEnd = 10
                lVBox.MarginTop = 10
                lVBox.MarginBottom = 10
                
                ' Name entry
                Dim lNameBox As New Box(Orientation.Horizontal, 10)
                lNameBox.PackStart(New Label("Name:"), False, False, 0)
                Dim lNameEntry As New Entry()
                lNameEntry.Text = "New Scratchpad"
                lNameBox.PackStart(lNameEntry, True, True, 0)
                lVBox.PackStart(lNameBox, False, False, 0)
                
                ' Scope radio buttons
                Dim lScopeBox As New Box(Orientation.Vertical, 5)
                Dim lGlobalRadio As New RadioButton("Global (available in all projects)")
                Dim lProjectRadio As New RadioButton(lGlobalRadio, "project (only in current project)")
                
                ' Disable project option if no project is open
                If String.IsNullOrEmpty(pScratchpadManager.GetType().GetField("pCurrentProjectPath", 
                    Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance).GetValue(pScratchpadManager).ToString()) Then
                    lProjectRadio.Sensitive = False
                End If
                
                lScopeBox.PackStart(lGlobalRadio, False, False, 0)
                lScopeBox.PackStart(lProjectRadio, False, False, 0)
                lVBox.PackStart(New Label("Scope:"), False, False, 0)
                lVBox.PackStart(lScopeBox, False, False, 0)
                
                lDialog.ContentArea.Add(lVBox)
                lDialog.ShowAll()
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lName As String = lNameEntry.Text.Trim()
                    If Not String.IsNullOrEmpty(lName) Then
                        Dim lScope As ScratchpadScope = If(lGlobalRadio.Active, 
                                                          ScratchpadScope.eGlobal, 
                                                          ScratchpadScope.eProject)
                        
                        ' Save current scratchpad first
                        SaveCurrentScratchpad()
                        
                        ' Create new scratchpad
                        Dim lNewScratchpad As ScratchpadData = pScratchpadManager.CreateScratchpad(lName, lScope)
                        
                        ' Reload list and select new one
                        LoadScratchpads()
                        
                        ' Find and select the new scratchpad
                        Dim lScratchpads As List(Of ScratchpadData) = pScratchpadManager.GetAllScratchpads()
                        For i As Integer = 0 To lScratchpads.Count - 1
                            If lScratchpads(i).Id = lNewScratchpad.Id Then
                                pScratchpadCombo.Active = i
                                LoadScratchpad(lNewScratchpad)
                                Exit For
                            End If
                        Next
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnNewScratchpad error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDeleteScratchpad(vSender As Object, vArgs As EventArgs)
            Try
                If pCurrentScratchpad Is Nothing Then Return
                
                ' Confirm deletion
                Dim lDialog As New MessageDialog(CType(Me.Toplevel, Window),
                                                DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                                MessageType.Question,
                                                ButtonsType.YesNo,
                                                $"Delete scratchpad '{pCurrentScratchpad.Name}'?")
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    ' Delete the scratchpad
                    pScratchpadManager.DeleteScratchpad(pCurrentScratchpad.Id)
                    
                    ' Select default or first available
                    SelectDefaultScratchpad()
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnDeleteScratchpad error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnCopy(vSender As Object, vArgs As EventArgs)
            Try
                Dim lBounds As TextIter = Nothing
                Dim lStart As TextIter = Nothing
                Dim lEnd As TextIter = Nothing
                
                If pTextBuffer.GetSelectionBounds(lStart, lEnd) Then
                    Dim lText As String = pTextBuffer.GetText(lStart, lEnd, True)
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", False))
                    lClipboard.Text = lText
                    UpdateStatus("Copied to clipboard")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnCopy error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnCut(vSender As Object, vArgs As EventArgs)
            Try
                Dim lStart As TextIter = Nothing
                Dim lEnd As TextIter = Nothing
                
                If pTextBuffer.GetSelectionBounds(lStart, lEnd) Then
                    Dim lText As String = pTextBuffer.GetText(lStart, lEnd, True)
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", False))
                    lClipboard.Text = lText
                    Dim lStartRef As TextIter = lStart
                    Dim lEndRef As TextIter = lEnd  
                    pTextBuffer.Text = pTextBuffer.GetText(pTextBuffer.StartIter, lStart, True) & _
                                pTextBuffer.GetText(lEnd, pTextBuffer.EndIter, True)
                    UpdateStatus("Cut to clipboard")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnCut error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnPaste(vSender As Object, vArgs As EventArgs)
            Try
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", False))
                Dim lText As String = lClipboard.WaitForText()
                
                If Not String.IsNullOrEmpty(lText) Then
                    pTextBuffer.InsertAtCursor(lText)
                    UpdateStatus("Pasted from clipboard")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnPaste error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnClear(vSender As Object, vArgs As EventArgs)
            Try
                ' Confirm clear
                Dim lDialog As New MessageDialog(CType(Me.Toplevel, Window),
                                                DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                                MessageType.Question,
                                                ButtonsType.YesNo,
                                                "Clear all Content?")
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    pTextBuffer.Text = ""
                    UpdateStatus("Content cleared")
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnClear error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnInsertDate(vSender As Object, vArgs As EventArgs)
            Try
                Dim lDateTime As String = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                pTextBuffer.InsertAtCursor($"{lDateTime}: ")
                UpdateStatus("Date/time inserted")
                
            Catch ex As Exception
                Console.WriteLine($"OnInsertDate error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnInsertTodo(vSender As Object, vArgs As EventArgs)
            Try
                pTextBuffer.InsertAtCursor("TODO: ")
                UpdateStatus("TODO marker inserted")
                
            Catch ex As Exception
                Console.WriteLine($"OnInsertTodo error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSearch(vSender As Object, vArgs As EventArgs)
            Try
                ' Create simple search dialog
                Dim lDialog As New Dialog("Search", 
                                        CType(Me.Toplevel, Window), 
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                        "Close", ResponseType.Close,
                                        "Find Next", ResponseType.Accept)
                
                lDialog.SetDefaultSize(400, 150)
                
                Dim lBox As New Box(Orientation.Horizontal, 10)
                lBox.MarginStart = 10
                lBox.MarginEnd = 10
                lBox.MarginTop = 10
                lBox.MarginBottom = 10
                
                lBox.PackStart(New Label("Search:"), False, False, 0)
                Dim lSearchEntry As New Entry()
                lBox.PackStart(lSearchEntry, True, True, 0)
                
                lDialog.ContentArea.Add(lBox)
                lDialog.ShowAll()
                
                ' Simple search implementation
                Dim lLastPos As TextIter = pTextBuffer.StartIter
                
                While lDialog.Run() = CInt(ResponseType.Accept)
                    Dim lSearchText As String = lSearchEntry.Text
                    If Not String.IsNullOrEmpty(lSearchText) Then
                        Dim lFoundPos As TextIter = Nothing
                        Dim lEndPos As TextIter = Nothing
                        
                        If lLastPos.ForwardSearch(lSearchText, TextSearchFlags.CaseInsensitive, 
                                                 lFoundPos, lEndPos, Nothing) Then
                            pTextBuffer.SelectRange(lFoundPos, lEndPos)
                            pTextView.ScrollToIter(lFoundPos, 0.1, False, 0, 0)
                            lLastPos = lEndPos
                            UpdateStatus($"Found: {lSearchText}")
                        Else
                            ' Wrap around to beginning
                            lLastPos = pTextBuffer.StartIter
                            UpdateStatus("Not found - wrapped to beginning")
                        End If
                    End If
                End While
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnSearch error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Hyperlink Detection =====
        
        Private Sub ConfigureHyperlinkDetection()
            Try
                ' Create tag for hyperlinks
                Dim lLinkTag As TextTag = pTextBuffer.TagTable.Lookup("hyperlink")
                If lLinkTag Is Nothing Then
                    lLinkTag = New TextTag("hyperlink")
                    lLinkTag.Foreground = "blue"
                    lLinkTag.Underline = Pango.Underline.Single
                    pTextBuffer.TagTable.Add(lLinkTag)
                End If
                
                ' Enable event handling for links
                pTextView.AddEvents(CInt(Gdk.EventMask.ButtonPressMask Or Gdk.EventMask.PointerMotionMask))
                
            Catch ex As Exception
                Console.WriteLine($"ConfigureHyperlinkDetection error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnTextViewButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                If vArgs.Event.Button <> 1 Then Return ' only handle left click
                
                ' Get iterator at click position
                Dim lX As Integer = CInt(vArgs.Event.x)
                Dim lY As Integer = CInt(vArgs.Event.y)
                Dim lIter As TextIter = Nothing
                
                pTextView.WindowToBufferCoords(TextWindowType.Widget, lX, lY, lX, lY)
                pTextView.GetIterAtLocation(lIter, lX, lY)
                
                ' Check if we're on a hyperlink
                Dim lUrl As String = ExtractUrlAtIter(lIter)
                If Not String.IsNullOrEmpty(lUrl) Then
                    LaunchUrl(lUrl)
                    vArgs.RetVal = True ' Handled
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTextViewButtonPress error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnTextViewMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs)
            Try
                ' Get iterator at mouse position
                Dim lX As Integer = CInt(vArgs.Event.x)
                Dim lY As Integer = CInt(vArgs.Event.y)
                Dim lIter As TextIter = Nothing
                
                pTextView.WindowToBufferCoords(TextWindowType.Widget, lX, lY, lX, lY)
                pTextView.GetIterAtLocation(lIter, lX, lY)
                
                ' Check if we're over a hyperlink
                Dim lUrl As String = ExtractUrlAtIter(lIter)
                If Not String.IsNullOrEmpty(lUrl) Then
                    Dim lDisplay As Gdk.display = Gdk.display.Default
                    pTextView.Window.Cursor = New Gdk.Cursor(lDisplay, Gdk.CursorType.Hand2)
                Else
                    pTextView.Window.Cursor = Nothing
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTextViewMotionNotify error: {ex.Message}")
            End Try
        End Sub
        
        Private Function ExtractUrlAtIter(vIter As TextIter) As String
            Try
                ' Simple URL detection - look for http:// or https://
                
                Dim lLine As Integer = vIter.Line()
                Dim lLineStart As TextIter = pTextBuffer.GetIterAtLine(lLine)
                Dim lLineEnd As TextIter = lLineStart
                lLineEnd.ForwardToLineEnd()
                
                Dim lText As String = pTextBuffer.GetText(lLineStart, lLineEnd, True)
                
                ' Regular expression for URL detection
                Dim lRegex As New Regex("https?://[^\s]+", RegexOptions.IgnoreCase)
                Dim lMatches As MatchCollection = lRegex.Matches(lText)
                
                For Each lMatch As Match In lMatches
                    Dim lStartOffset As Integer = lLineStart.Offset + lMatch.Index
                    Dim lEndOffset As Integer = lStartOffset + lMatch.Length
                    
                    If vIter.Offset >= lStartOffset AndAlso vIter.Offset <= lEndOffset Then
                        Return lMatch.Value
                    End If
                Next
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"ExtractUrlAtIter error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        Private Sub LaunchUrl(vUrl As String)
            Try
                Process.Start(New ProcessStartInfo() With {
                    .FileName = vUrl,
                    .UseShellExecute = True
                })
                UpdateStatus($"Launched: {vUrl}")
                
            Catch ex As Exception
                Console.WriteLine($"LaunchUrl error: {ex.Message}")
                UpdateStatus($"Failed to launch Url: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        Private Sub ApplyFontSettings()
            Try
                ' Apply monospace font for code snippets
                Dim lFontDesc As Pango.FontDescription = Pango.FontDescription.FromString("Monospace 10")
                CssHelper.ApplyCssToWidget(pTextView, "textview { font-family: ...; }", CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                
            Catch ex As Exception
                Console.WriteLine($"ApplyFontSettings error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateStatus(vMessage As String)
            Try
                If pStatusLabel IsNot Nothing Then
                    pStatusLabel.Text = vMessage
                End If
            Finally
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        Public Sub RefreshScratchpads()
            LoadScratchpads()
        End Sub
        
        Public Sub SetProjectPath(vProjectPath As String)
            ' Save current scratchpad before switching context
            SaveCurrentScratchpad()
            
            ' Update manager's project path
            pScratchpadManager.SetProjectPath(vProjectPath)
            
            ' Reload scratchpads
            LoadScratchpads()
            
            ' Select default
            SelectDefaultScratchpad()
        End Sub
        
        Public Sub ForceSave()
            SaveCurrentScratchpad()
        End Sub
        
    End Class
    
End Namespace
