' MainWindow.StatusBar.vb
' Created: 2025-08-04 08:31:10

Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities


Partial Public Class MainWindow

    ' Add new private field for the line number entry
    Private pLineNumberEntry As Entry
    Private pColumnLabel As Label
    Private pLineNumberUpdateInProgress As Boolean = False
    ' Status Bar private fields
    Private pLineColumnLabel As Label
    Private pEncodingLabel As Label
    Private pLanguageLabel As Label
    Private pFileTypeLabel As Label
    Private pBuildStatusLabel As Label
    Private pProgressBar As ProgressBar


    Private Sub CreateStatusBar()
        ' Create main status bar container
        Dim lStatusContainer As New Box(Orientation.Horizontal, 0)
        lStatusContainer.SetSizeRequest(-1, 20) ' Minimum Height
        lStatusContainer.HeightRequest = 20     ' Explicit Height request
        
        ' Use default GTK theme colors instead of blue
        Dim lCss As String = "box { " & _
            "padding-top: 1px; " & _
            "padding-bottom: 1px; " & _
            "padding-left: 8px; " & _
            "padding-right: 8px; " & _
            "min-height: 20px; " & _
            "border-top: 1px solid @borders; " & _  ' Add subtle top border
            "}"
        CssHelper.ApplyCssToWidget(lStatusContainer, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' LEFT SIDE: Main status message
        pStatusBar = New Statusbar()
        pStatusBar.Push(0, "Ready")
        
        ' Remove white color, use default theme colors
        Dim lStatusCss As String = "statusbar { " & _
            "border: none; " & _
            "min-height: 18px; " & _
            "font-size: 14px; " & _
            "}"
        CssHelper.ApplyCssToWidget(pStatusBar, lStatusCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Build status components (middle-left)
        pBuildStatusLabel = New Label("")
        pBuildStatusLabel.Visible = False
        pBuildStatusLabel.Halign = Align.Start
        pBuildStatusLabel.MarginStart = 10
        
        ' Style build status label
        Dim lBuildStatusCss As String = "label { font-size: 11px; }"
        CssHelper.ApplyCssToWidget(pBuildStatusLabel, lBuildStatusCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        pProgressBar = New ProgressBar()
        pProgressBar.SetSizeRequest(100, 14)  ' Reduced Height from 16 to 14
        pProgressBar.Visible = False
        pProgressBar.MarginStart = 10
        pProgressBar.MarginEnd = 10
        
        ' RIGHT SIDE PANELS: Create separate labels for different info
        Dim lRightPanelsBox As New Box(Orientation.Horizontal, 0)
        
        ' Style for all right panel labels
        Dim lPanelLabelCss As String = "label { font-size: 11px; }"
        Dim lEntryBoxCss As String = "entry { font-size: 11px; min-height: 16px; padding: 0px 4px; }"
        
        ' Line/Column indicator - Now split into Entry and Label
        Dim lLineColumnBox As New Box(Orientation.Horizontal, 4)
        
        ' "Ln" prefix label
        Dim lLnLabel As New Label("Ln ")
        lLnLabel.Halign = Align.Center
        CssHelper.ApplyCssToWidget(lLnLabel, lPanelLabelCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Line number entry
        pLineNumberEntry = New Entry()
        pLineNumberEntry.Text = "1"
        pLineNumberEntry.WidthChars = 5
        pLineNumberEntry.MaxLength = 6  ' Support up to 999999 lines
        pLineNumberEntry.Halign = Align.Center
        CssHelper.ApplyCssToWidget(pLineNumberEntry, lEntryBoxCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Connect events for line number entry
        AddHandler pLineNumberEntry.Activated, AddressOf OnLineNumberEntryActivated
        AddHandler pLineNumberEntry.FocusOutEvent, AddressOf OnLineNumberEntryFocusOut
        AddHandler pLineNumberEntry.KeyPressEvent, AddressOf OnLineNumberEntryKeyPress
        
        ' Column label
        pColumnLabel = New Label(", Col 1")
        pColumnLabel.Halign = Align.Center
        CssHelper.ApplyCssToWidget(pColumnLabel, lPanelLabelCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        lLineColumnBox.PackStart(lLnLabel, False, False, 0)
        lLineColumnBox.PackStart(pLineNumberEntry, False, False, 0)
        lLineColumnBox.PackStart(pColumnLabel, False, False, 0)
        lLineColumnBox.MarginStart = 10
        lLineColumnBox.MarginEnd = 10
        
        ' File encoding indicator  
        pEncodingLabel = New Label("UTF-8")
        pEncodingLabel.Halign = Align.Center
        pEncodingLabel.MarginStart = 10
        pEncodingLabel.MarginEnd = 10
        pEncodingLabel.SetSizeRequest(50, -1)
        CssHelper.ApplyCssToWidget(pEncodingLabel, lPanelLabelCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Language mode indicator
        pLanguageLabel = New Label("VB.NET")
        pLanguageLabel.Halign = Align.Center
        pLanguageLabel.MarginStart = 10
        pLanguageLabel.MarginEnd = 10
        pLanguageLabel.SetSizeRequest(60, -1)
        CssHelper.ApplyCssToWidget(pLanguageLabel, lPanelLabelCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Git status indicator
        pGitStatusLabel = New Label()
        pGitStatusLabel.Halign = Align.Center
        pGitStatusLabel.MarginStart = 10
        pGitStatusLabel.MarginEnd = 10
        pGitStatusLabel.SetSizeRequest(80, -1)
        pGitStatusLabel.Visible = False  ' Hidden by default
        CssHelper.ApplyCssToWidget(pGitStatusLabel, lPanelLabelCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Make git label clickable
        Dim lGitEventBox As New EventBox()
        lGitEventBox.Add(pGitStatusLabel)
        lGitEventBox.VisibleWindow = False
        
        ' Use proper event handler syntax
        AddHandler lGitEventBox.ButtonPressEvent, Sub(s As Object, e As ButtonPressEventArgs)
            If pGitStatusLabel.Visible Then
                OnShowGitStatus(Nothing, EventArgs.Empty)
            End If
            ' Mark event as handled
            e.RetVal = True
        End Sub
        
       ' Handle mouse enter to show hand cursor
        AddHandler lGitEventBox.EnterNotifyEvent, Sub(s As Object, e As EnterNotifyEventArgs)
            If pGitStatusLabel.Visible Then
                Try
                    ' Get the window first, then set cursor
                    Dim lWindow As Gdk.Window = lGitEventBox.Window
                    If lWindow IsNot Nothing Then
                        ' Use Gdk.Display.Default as a shared property (not through instance)
                        Dim lDisplay As Gdk.display = Gdk.display.Default
                        Dim lCursor As New Gdk.Cursor(lDisplay, Gdk.CursorType.Hand2)
                        lWindow.Cursor = lCursor
                    End If
                Catch ex As Exception
                    ' Ignore cursor errors
                    Console.WriteLine($"Warning: Could not set cursor: {ex.Message}")
                End Try
            End If
            ' Don't mark as handled to allow event propagation
            e.RetVal = False
        End Sub
        
        ' Handle mouse leave to restore default cursor
        AddHandler lGitEventBox.LeaveNotifyEvent, Sub(s As Object, e As LeaveNotifyEventArgs)
            Try
                ' Get the window and reset cursor
                Dim lWindow As Gdk.Window = lGitEventBox.Window
                If lWindow IsNot Nothing Then
                    lWindow.Cursor = Nothing
                End If
            Catch ex As Exception
                ' Ignore cursor errors
                Console.WriteLine($"Warning: Could not reset cursor: {ex.Message}")
            End Try
            ' Don't mark as handled to allow event propagation
            e.RetVal = False
        End Sub
        
        ' File type indicator (hidden by default, shown when file is open)
        pFileTypeLabel = New Label()
        pFileTypeLabel.Halign = Align.Center
        pFileTypeLabel.MarginStart = 10
        pFileTypeLabel.MarginEnd = 10
        pFileTypeLabel.SetSizeRequest(50, -1)
        pFileTypeLabel.Visible = False
        CssHelper.ApplyCssToWidget(pFileTypeLabel, lPanelLabelCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Create separators with vertical lines
        Dim lSeparatorCss As String = "separator { " & _
            "min-width: 1px; " & _
            "background-color: @borders; " & _
            "}"
        
        Dim lSeparator1 As New Separator(Orientation.Vertical)
        CssHelper.ApplyCssToWidget(lSeparator1, lSeparatorCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        Dim lSeparator2 As New Separator(Orientation.Vertical)
        CssHelper.ApplyCssToWidget(lSeparator2, lSeparatorCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        Dim lSeparator3 As New Separator(Orientation.Vertical)
        CssHelper.ApplyCssToWidget(lSeparator3, lSeparatorCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
        
        ' Pack right side panels
        lRightPanelsBox.PackStart(lSeparator1, False, False, 0)
        lRightPanelsBox.PackStart(lLineColumnBox, False, False, 0)
        lRightPanelsBox.PackStart(lSeparator2, False, False, 0)
        lRightPanelsBox.PackStart(pLanguageLabel, False, False, 0)
        lRightPanelsBox.PackStart(lSeparator3, False, False, 0)
        lRightPanelsBox.PackStart(lGitEventBox, False, False, 0)
        lRightPanelsBox.PackStart(lSeparator3, False, False, 0)
        lRightPanelsBox.PackStart(pEncodingLabel, False, False, 0)
        
        ' Pack everything into main status container
        lStatusContainer.PackStart(pStatusBar, True, True, 0)              ' Left: expandable Status
        lStatusContainer.PackStart(pBuildStatusLabel, False, False, 0)     ' Middle-left: build Status
        lStatusContainer.PackStart(pProgressBar, False, False, 0)          ' Middle-left: Progress
        lStatusContainer.PackEnd(lRightPanelsBox, False, False, 0)         ' Right: info panels
        
        ' Pack the status container at the END (bottom) of main vbox
        pMainVBox.PackEnd(lStatusContainer, False, False, 0)

        ' Ensure it's visible
        lStatusContainer.Show()
    End Sub
    
    ' Updated UpdateCursorPosition to work with Entry widget
    Private Sub UpdateCursorPosition(vTab As TabInfo)
        Try
            If pLineNumberEntry IsNot Nothing AndAlso pColumnLabel IsNot Nothing AndAlso vTab IsNot Nothing Then
                Dim lLine As Integer = vTab.Editor.CurrentLine + 1  ' Convert to 1-based
                Dim lColumn As Integer = vTab.Editor.CurrentColumn + 1  ' Convert to 1-based
                
                ' Update line number entry without triggering navigation
                pLineNumberUpdateInProgress = True
                pLineNumberEntry.Text = lLine.ToString()
                pLineNumberUpdateInProgress = False
                
                ' Update column label
                pColumnLabel.Text = $", Col {lColumn}"
            End If
        Catch ex As Exception
            Console.WriteLine($"error updating cursor position: {ex.Message}")
        End Try
    End Sub
    
    ' Event handler when Enter is pressed in line number entry
    Private Sub OnLineNumberEntryActivated(vSender As Object, vArgs As EventArgs)
        Try
            NavigateToLineFromEntry()
        Catch ex As Exception
            Console.WriteLine($"error navigating to Line: {ex.Message}")
        End Try
    End Sub
    
    ' Event handler when line number entry loses focus
    Private Sub OnLineNumberEntryFocusOut(vSender As Object, vArgs As FocusOutEventArgs)
        Try
            ' Restore current line number if invalid input
            Dim lTab As TabInfo = GetCurrentTabInfo()
            If lTab IsNot Nothing Then
                UpdateCursorPosition(lTab)
            End If
        Catch ex As Exception
            Console.WriteLine($"error handling focus out: {ex.Message}")
        End Try
    End Sub
    
    ' Event handler for key presses in line number entry
    Private Sub OnLineNumberEntryKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
        Try
            ' Allow only digits, backspace, delete, and navigation keys
            Dim lKey As Gdk.key = vArgs.Event.key
            
            Select Case lKey
                Case Gdk.key.Key_0 To Gdk.key.Key_9,
                     Gdk.key.KP_0 To Gdk.key.KP_9,
                     Gdk.key.BackSpace,
                     Gdk.key.Delete,
                     Gdk.key.Left,
                     Gdk.key.Right,
                     Gdk.key.Home,
                     Gdk.key.End,
                     Gdk.key.Tab,
                     Gdk.key.ISO_Left_Tab
                    ' Allow these keys
                    vArgs.RetVal = False
                    
                Case Gdk.key.Escape
                    ' ESC pressed - return focus to editor
                    Dim lTab As TabInfo = GetCurrentTabInfo()
                    If lTab IsNot Nothing Then
                        ' Restore current line number
                        UpdateCursorPosition(lTab)
                        ' Return focus to the code editor
                        lTab.Editor.GrabFocus()
                    End If
                    vArgs.RetVal = True
                    
                Case Else
                    ' Block all other keys
                    vArgs.RetVal = True
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"error handling key press: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Focuses the line number entry with visual feedback and text selection
    ''' </summary>
    Public Sub FocusLineNumberEntry()
        Try
            If pLineNumberEntry IsNot Nothing Then
                ' Apply a highlight CSS to show it has focus
                Dim lHighlightCss As String = "entry { " & _
                    "background-color: #FFF3CD; " & _
                    "color: #000000; " & _
                    "border: 2px solid #007ACC; " & _
                    "font-weight: bold; " & _
                    "}"
                CssHelper.ApplyCssToWidget(pLineNumberEntry, lHighlightCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                
                ' Focus the entry
                pLineNumberEntry.GrabFocus()
                
                ' Select all text so user can type to replace
                pLineNumberEntry.SelectRegion(0, -1)
                
                ' Update status bar message to inform user
                Dim lContext As UInteger = pStatusBar.GetContextId("goto")
                pStatusBar.Push(lContext, "Enter line number and press Enter (Esc to cancel)")
            End If
        Catch ex As Exception
            Console.WriteLine($"Error focusing line number entry: {ex.Message}")
        End Try
    End Sub
    
    ' Navigate to the line number entered in the textbox
    Private Sub NavigateToLineFromEntry()
        Try
            If pLineNumberUpdateInProgress Then Return
            
            Dim lTab As TabInfo = GetCurrentTabInfo()
            If lTab Is Nothing Then Return
            
            Dim lTargetLine As Integer
            If Integer.TryParse(pLineNumberEntry.Text, lTargetLine) Then
                ' Validate line number
                lTab.Editor.GoToLine(lTargetLine)
                lTab.Editor.GrabFocus()
                
            Else
                ' Invalid input - restore current line
                UpdateCursorPosition(lTab)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"error navigating to Line: {ex.Message}")
            ' Restore current line on error
            Try
                Dim lTab As TabInfo = GetCurrentTabInfo()
                If lTab IsNot Nothing Then UpdateCursorPosition(lTab)
            Catch
                ' Ignore errors during recovery
            End Try
        End Try
    End Sub

    
    
    Private Sub UpdateStatusBar()
        Try
            ' Update position information
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab?.Editor IsNot Nothing Then
                Dim lLine As Integer = lCurrentTab.Editor.CurrentLine
                Dim lColumn As Integer = lCurrentTab.Editor.CurrentColumn
                
                Dim lPositionContext As UInteger = pStatusBar.GetContextId("position")
                pStatusBar.Pop(lPositionContext)
                pStatusBar.Push(lPositionContext, $"Line {lLine + 1}, Column {lColumn + 1}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateStatusBar error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Update status bar text
    ''' </summary>
    Private Sub UpdateStatusBar(vMessage As String)
        Try
            ' Update status bar with custom message
            Dim lMainContext As UInteger = pStatusBar.GetContextId("Main")
            pStatusBar.Pop(lMainContext)
            pStatusBar.Push(lMainContext, vMessage)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateStatusBar error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Updates the progress bar percentage
    ''' </summary>
    ''' <param name="vPercentage">Percentage complete (0-100)</param>
    Private Sub UpdateProgressBar(vPercentage As Double)
        Try
            If pProgressBar Is Nothing Then Return
            
            ' Clamp to valid range
            If vPercentage < 0 Then vPercentage = 0
            If vPercentage > 100 Then vPercentage = 100
            
            ' Convert to fraction (0-1)
            pProgressBar.Fraction = vPercentage / 100.0
            
            ' Force immediate update
            While Application.EventsPending()
                Application.RunIteration(False)
            End While
            
        Catch ex As Exception
            Console.WriteLine($"UpdateProgressBar error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Shows or hides the progress bar in the status bar
    ''' </summary>
    ''' <param name="vShow">True to show, False to hide</param>
    Private Sub ShowProgressBar(vShow As Boolean)
        Try
            If pProgressBar Is Nothing Then Return
            
            pProgressBar.Visible = vShow
            
            If vShow Then
                pProgressBar.Fraction = 0
                pProgressBar.ShowAll()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ShowProgressBar error: {ex.Message}")
        End Try
    End Sub

End Class

