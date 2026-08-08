' FindReplacePanel.vb - Complete Find/Replace panel implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

Namespace Widgets
    Public Class FindReplacePanel
        Inherits Box

        ' UI Controls
        Private pFindEntry As CustomDrawTextBox
        Private pReplaceEntry As CustomDrawTextBox
        Private pFindButton As CustomDrawButton
        Private pReplaceButton As CustomDrawButton
        Private pReplaceAllButton As CustomDrawButton
        Private pFindNextButton As CustomDrawButton
        Private pFindPreviousButton As CustomDrawButton
        Private pCloseButton As CustomDrawButton
        Private pCaseSensitiveCheck As CustomDrawCheckBox
        Private pWholeWordCheck As CustomDrawCheckBox
        Private pRegexCheck As CustomDrawCheckBox
        Private pInFileRadio As CustomDrawCheckBox
        Private pInProjectRadio As CustomDrawCheckBox
        Private pUpdatingScopeToggle As Boolean = False
        Private pStatusLabel As Label
        Private pProgressBar As ProgressBar
        Private pCancelButton As CustomDrawButton
        ''' <summary>
        ''' Custom-drawn, multi-column results grid - replaced a native Gtk.TreeView, which
        ''' only picked up two hardcoded dark/light color pairs from ThemeManager's generic
        ''' CSS instead of the actual active theme. Reads EditorTheme colors directly, no
        ''' GTK CSS involved.
        ''' </summary>
        Private pResultsGrid As CustomDrawDataGrid

        ''' <summary>
        ''' QuickFind button positioned to the left of Find label
        ''' </summary>
        Private pQuickFindButton As CustomDrawButton
        
        
        ' Search state
        Private pProjectRoot As String
        Private pSearchResults As New List(Of FindResult)()
        Private pIsSearching As Boolean = False
        Private pLastSearchOptions As SearchOptions

        ' Current file search state
        Private pCurrentMatches As List(Of EditorPosition)
        Private pCurrentMatchIndex As Integer = -1

        ' Events
        Public Event OnRequestCurrentTab(vTabInfoEventArgs As TabInfoEventArgs)
        Public Event ResultSelected(vFilePath As String, vLineNumber As Integer, vColumnNumber As Integer)
        Public Event OpenFileRequested(vFilePath As String)
        Public Event CloseRequested()
        Public Event OnRequestOpenTabs As EventHandler(Of OpenTabsEventArgs)

        ' Helper classes
        Public Class TabInfoEventArgs
            Inherits EventArgs
            Public Property TabInfo As TabInfo
        End Class
        
        Public Class OpenTabsEventArgs
            Inherits EventArgs
            Public Property OpenTabs As List(Of TabInfo)
        End Class

        ' Search configuration
        Public Structure SearchOptions
            Public SearchText As String
            Public ReplaceText As String
            Public MatchCase As Boolean
            Public WholeWord As Boolean
            Public UseRegex As Boolean
            Public Scope As SearchScope
            Public FileFilter As String
        End Structure

        Public Enum SearchScope
            eUnspecified
            eCurrentFile
            eProject
            eLastValue
        End Enum
        
        Public Class FindResult
            ' Core properties
            Public Property FilePath As String
            Public Property LineNumber As Integer
            Public Property ColumnNumber As Integer
            Public Property LineText As String
            Public Property MatchText As String
            Public Property MatchLength As Integer
            
            ''' <summary>
            ''' Parameterized constructor for FindResult
            ''' </summary>
            ''' <param name="vFilePath">Full path to the file containing the match</param>
            ''' <param name="vLineNumber">Line number of the match (1-based)</param>
            ''' <param name="vColumnNumber">Column number of the match (1-based)</param>
            ''' <param name="vLineText">The text of the line containing the match</param>
            ''' <param name="vMatchColumnInLine">Column offset of the match within the line (0-based)</param>
            ''' <param name="vMatchLength">Length of the matched text</param>
            Public Sub New(vFilePath As String, 
                           vLineNumber As Integer, 
                           vColumnNumber As Integer, 
                           vLineText As String, 
                           vMatchColumnInLine As Integer, 
                           vMatchLength As Integer)
                
                Me.FilePath = vFilePath
                Me.LineNumber = vLineNumber
                Me.ColumnNumber = vColumnNumber
                Me.LineText = vLineText
                Me.MatchLength = vMatchLength
                
                ' Extract the match text from the line using the column offset
                Try
                    If Not String.IsNullOrEmpty(vLineText) AndAlso 
                       vMatchColumnInLine >= 0 AndAlso 
                       vMatchColumnInLine + vMatchLength <= vLineText.Length Then
                        
                        Me.MatchText = vLineText.Substring(vMatchColumnInLine, vMatchLength)
                    Else
                        ' Fallback if we can't extract the exact match
                        Me.MatchText = ""
                    End If
                Catch ex As Exception
                    Console.WriteLine($"FindResult constructor error extracting match text: {ex.Message}")
                    Me.MatchText = ""
                End Try
            End Sub
            
            ''' <summary>
            ''' Default parameterless constructor (keeps existing functionality)
            ''' </summary>
            Public Sub New()
                ' Default constructor for object initializer syntax
            End Sub
            
            ''' <summary>
            ''' Gets the file name without path
            ''' </summary>
            Public ReadOnly Property FileName As String
                Get
                    If String.IsNullOrEmpty(FilePath) Then
                        Return ""
                    End If
                    Return System.IO.Path.GetFileName(FilePath)
                End Get
            End Property
            
            ''' <summary>
            ''' Gets the directory path
            ''' </summary>
            Public ReadOnly Property DirectoryPath As String
                Get
                    If String.IsNullOrEmpty(FilePath) Then
                        Return ""
                    End If
                    Return System.IO.Path.GetDirectoryName(FilePath)
                End Get
            End Property
            
            ''' <summary>
            ''' Gets a display string for the result
            ''' </summary>
            Public ReadOnly Property DisplayText As String
                Get
                    Return $"{FileName}:{LineNumber}:{ColumnNumber}: {LineText}"
                End Get
            End Property
            
            ''' <summary>
            ''' Gets a tooltip-friendly description
            ''' </summary>
            Public ReadOnly Property ToolTipText As String
                Get
                    Return $"File: {FilePath}{Environment.NewLine}" &
                           $"Line {LineNumber}, Column {ColumnNumber}{Environment.NewLine}" &
                           $"Match: ""{MatchText}"" ({MatchLength} characters)"
                End Get
            End Property

        

            
            ''' <summary>
            ''' Compares two FindResult objects for equality
            ''' </summary>
            Public Overrides Function Equals(obj As Object) As Boolean
                If obj Is Nothing OrElse Not TypeOf obj Is FindResult Then
                    Return False
                End If
                
                Dim lOther As FindResult = DirectCast(obj, FindResult)
                Return FilePath = lOther.FilePath AndAlso
                       LineNumber = lOther.LineNumber AndAlso
                       ColumnNumber = lOther.ColumnNumber AndAlso
                       MatchText = lOther.MatchText
            End Function
            
            ''' <summary>
            ''' Gets hash code for the result
            ''' </summary>
            Public Overrides Function GetHashCode() As Integer
                Return HashCode.Combine(FilePath, LineNumber, ColumnNumber, MatchText)
            End Function
            
            ''' <summary>
            ''' String representation for debugging
            ''' </summary>
            Public Overrides Function ToString() As String
                Return DisplayText
            End Function
        End Class

        Public Sub New()
            MyBase.New(Orientation.Vertical, 5)
            InitializeUI()
            ConnectEvents()
            InitializeEscapeHandling()
        End Sub

        ''' <summary>
        ''' Applies the app's color theme to every control this panel owns. Each CustomDraw*
        ''' control (including pResultsGrid) self-subscribes to ThemeManager.ThemeChanged and
        ''' redraws itself directly from real EditorTheme colors, so this only needs to hand
        ''' out the reference once - no GTK CSS override needed, unlike the Gtk.TreeView this
        ''' results grid replaced.
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pFindEntry.ThemeManager = vThemeManager
                pReplaceEntry.ThemeManager = vThemeManager

                pFindButton.ThemeManager = vThemeManager
                pReplaceButton.ThemeManager = vThemeManager
                pReplaceAllButton.ThemeManager = vThemeManager
                pFindNextButton.ThemeManager = vThemeManager
                pFindPreviousButton.ThemeManager = vThemeManager
                pCloseButton.ThemeManager = vThemeManager
                pCancelButton.ThemeManager = vThemeManager
                pQuickFindButton.ThemeManager = vThemeManager

                pCaseSensitiveCheck.ThemeManager = vThemeManager
                pWholeWordCheck.ThemeManager = vThemeManager
                pRegexCheck.ThemeManager = vThemeManager
                pInFileRadio.ThemeManager = vThemeManager
                pInProjectRadio.ThemeManager = vThemeManager

                pResultsGrid?.SetThemeManager(vThemeManager)

            Catch ex As Exception
                Console.WriteLine($"FindReplacePanel.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Initializes the user interface components of the find/replace panel with sortable results
        ''' </summary>
        Private Sub InitializeUI()
            Try
                ' Search/Replace controls
                Dim lSearchControls As Widget = CreateSearchControls()
                PackStart(lSearchControls, False, False, 0)
                
                ' Options
                Dim lOptionsControls As Widget = CreateOptionsControls()
                PackStart(lOptionsControls, False, False, 0)
                
                ' Status and progress
                Dim lStatusBox As New Box(Orientation.Horizontal, 5)
                pStatusLabel = New Label("Ready")
                pProgressBar = New ProgressBar()
                pProgressBar.Visible = False
                
                ' FIXED: pCancelButton is created in CreateSearchControls, so check if it exists
                If pCancelButton IsNot Nothing Then
                    pCancelButton.Visible = False
                End If
                
                lStatusBox.PackStart(pStatusLabel, False, False, 0)
                lStatusBox.PackEnd(pProgressBar, False, False, 0)
                PackStart(lStatusBox, False, False, 0)
                
                ' Results grid - self-contained, has its own scrollbar (no ScrolledWindow needed)
                pResultsGrid = CreateResultsGrid()
                PackStart(pResultsGrid, True, True, 0)
                
                ' Initialize
                UpdateButtonStates()
                ShowAll()
                
                ' Hide cancel button after ShowAll()
                If pCancelButton IsNot Nothing Then
                    pCancelButton.Visible = False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Error initializing FindReplacePanel: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Initialize ESC key handling for FindReplacePanel
        ''' </summary>
        ''' <remarks>
        ''' Call this in the constructor after creating all widgets
        ''' </remarks>
        Private Sub InitializeEscapeHandling()
            Try
                ' Connect ESC handler to search entry
                If pFindEntry IsNot Nothing Then
                    AddHandler pFindEntry.InnerEntry.KeyPressEvent, AddressOf OnFindPanelKeyPress
                End If
                
                ' Connect ESC handler to replace entry
                If pReplaceEntry IsNot Nothing Then
                    AddHandler pReplaceEntry.InnerEntry.KeyPressEvent, AddressOf OnFindPanelKeyPress
                End If
                
                ' Connect ESC handler to the results grid's actual key-focused widget
                If pResultsGrid IsNot Nothing Then
                    AddHandler pResultsGrid.ContentArea.KeyPressEvent, AddressOf OnFindPanelKeyPress
                End If
                
                Console.WriteLine("FindReplacePanel: ESC handling initialized")
                
            Catch ex As Exception
                Console.WriteLine($"InitializeEscapeHandling error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Creates search controls with QuickFind button to the left of Find label
        ''' </summary>
        Private Function CreateSearchControls() As Widget
            Dim lMainBox As New Box(Orientation.Vertical, 5)
            
            ' First row: QuickFind button, Find entry and buttons
            Dim lFindBox As New Box(Orientation.Horizontal, 5)
            
            ' NEW: QuickFind button to the LEFT of Find label
            Dim lQuickFindIcon As Gdk.Pixbuf = Nothing
            Try
                lQuickFindIcon = Gtk.IconTheme.Default.LoadIcon("edit-find", 16, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"QuickFind icon load error: {ex.Message}")
            End Try
            pQuickFindButton = New CustomDrawButton("", lQuickFindIcon)
            pQuickFindButton.TooltipText = "Quick Find (Ctrl+F)"

            ' Pack QuickFind button FIRST (leftmost)
            lFindBox.PackStart(pQuickFindButton, False, False, 0)
            
            ' Now add the Find label
            Dim lFindLabel As New Label("Find:")
            lFindLabel.SetSizeRequest(80, -1)
            lFindLabel.Xalign = 0
            lFindBox.PackStart(lFindLabel, False, False, 0)
            
            pFindEntry = New CustomDrawTextBox()
            pFindEntry.PlaceholderText = "Enter search text..."
            lFindBox.PackStart(pFindEntry, True, True, 0)
            
            pFindButton = New CustomDrawButton("Find All")
            pFindNextButton = New CustomDrawButton("Next")
            pFindPreviousButton = New CustomDrawButton("Previous")
            
            lFindBox.PackStart(pFindButton, False, False, 0)
            lFindBox.PackStart(pFindNextButton, False, False, 0)
            lFindBox.PackStart(pFindPreviousButton, False, False, 0)
            
            lMainBox.PackStart(lFindBox, False, False, 0)
            
            ' Second row: Replace entry and buttons
            Dim lReplaceBox As New Box(Orientation.Horizontal, 5)
            
            ' Add spacer to align with Find row (width of QuickFind button + spacing)
            Dim lSpacer As New Label("")
            lSpacer.SetSizeRequest(28, -1)  ' Approximate width of QuickFind button
            lReplaceBox.PackStart(lSpacer, False, False, 0)
            
            Dim lReplaceLabel As New Label("Replace:")
            lReplaceLabel.SetSizeRequest(80, -1)
            lReplaceLabel.Xalign = 0
            lReplaceBox.PackStart(lReplaceLabel, False, False, 0)
            
            pReplaceEntry = New CustomDrawTextBox()
            pReplaceEntry.PlaceholderText = "Enter replacement text..."
            lReplaceBox.PackStart(pReplaceEntry, True, True, 0)
            
            pReplaceButton = New CustomDrawButton("Replace")
            pReplaceAllButton = New CustomDrawButton("Replace All")
            pCancelButton = New CustomDrawButton("Cancel")
            pCloseButton = New CustomDrawButton("Close")
            
            lReplaceBox.PackStart(pReplaceButton, False, False, 0)
            lReplaceBox.PackStart(pReplaceAllButton, False, False, 0)
            lReplaceBox.PackStart(pCancelButton, False, False, 0)
            lReplaceBox.PackStart(pCloseButton, False, False, 0)
            
            lMainBox.PackStart(lReplaceBox, False, False, 0)
            
            Return lMainBox
        End Function

        ''' <summary>
        ''' Handles QuickFind button click - focuses search entry and selects all text
        ''' </summary>
        Private Sub OnQuickFindClicked(vSender As Object, vE As EventArgs)
            Try
                PerformQuickFind
            Catch ex As Exception
                Console.WriteLine($"OnQuickFindClicked error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets up the QuickFind button to respond to keyboard shortcuts from MainWindow
        ''' </summary>
        ''' <remarks>
        ''' This allows MainWindow to trigger the QuickFind button when Ctrl+F is pressed
        ''' </remarks>
        Public Sub TriggerQuickFind()
            Try
                ' Simulate clicking the QuickFind button
                If pQuickFindButton IsNot Nothing Then
                    OnQuickFindClicked(pQuickFindButton, EventArgs.Empty)
                Else
                    ' Fallback to just focusing the search entry
                    FocusSearchEntry()
                End If
            Catch ex As Exception
                Console.WriteLine($"TriggerQuickFind error: {ex.Message}")
            End Try
        End Sub
        
        Private Function CreateOptionsControls() As Widget
            Dim lOptionsBox As New Box(Orientation.Horizontal, 10)
            
            ' Search options
            Dim lOptionsFrame As New Frame("Options")
            Dim lOptionsInnerBox As New Box(Orientation.Horizontal, 5)
            lOptionsInnerBox.MarginTop = 5
            lOptionsInnerBox.MarginBottom = 5
            lOptionsInnerBox.MarginStart = 5
            lOptionsInnerBox.MarginEnd = 5
            
            pCaseSensitiveCheck = New CustomDrawCheckBox("Case sensitive")
            pWholeWordCheck = New CustomDrawCheckBox("Whole word")
            pRegexCheck = New CustomDrawCheckBox("Use regex")
            
            lOptionsInnerBox.PackStart(pCaseSensitiveCheck, False, False, 0)
            lOptionsInnerBox.PackStart(pWholeWordCheck, False, False, 0)
            lOptionsInnerBox.PackStart(pRegexCheck, False, False, 0)
            
            lOptionsFrame.Add(lOptionsInnerBox)
            lOptionsBox.PackStart(lOptionsFrame, False, False, 0)
            
            ' Scope options
            Dim lScopeFrame As New Frame("Scope")
            Dim lScopeInnerBox As New Box(Orientation.Horizontal, 5)
            lScopeInnerBox.MarginTop = 5
            lScopeInnerBox.MarginBottom = 5
            lScopeInnerBox.MarginStart = 5
            lScopeInnerBox.MarginEnd = 5
            
            pInFileRadio = New CustomDrawCheckBox("current file")
            pInProjectRadio = New CustomDrawCheckBox("Entire project")
            pInFileRadio.Active = True
            
            lScopeInnerBox.PackStart(pInFileRadio, False, False, 0)
            lScopeInnerBox.PackStart(pInProjectRadio, False, False, 0)
            
            lScopeFrame.Add(lScopeInnerBox)
            lOptionsBox.PackStart(lScopeFrame, False, False, 0)
            
            Return lOptionsBox
        End Function

        ' Replace: SimpleIDE.FindReplacePanel.ConnectEvents
        ''' <summary>
        ''' Connects all event handlers including the new QuickFind button
        ''' </summary>
        Private Sub ConnectEvents()
            Try
                ' Find entry events
                AddHandler pFindEntry.Changed, AddressOf OnFindEntryChanged
                AddHandler pFindEntry.Activated, AddressOf OnFindEntryActivated
                AddHandler pFindEntry.InnerEntry.KeyPressEvent, AddressOf OnFindEntryKeyPress
                
                ' Replace entry events
                AddHandler pReplaceEntry.Activated, AddressOf OnReplaceEntryActivated
                
                ' Button events
                AddHandler pFindButton.Clicked, AddressOf OnFind
                AddHandler pFindNextButton.Clicked, AddressOf OnFindNext
                AddHandler pFindPreviousButton.Clicked, AddressOf OnFindPrevious
                AddHandler pReplaceButton.Clicked, AddressOf OnReplace
                AddHandler pReplaceAllButton.Clicked, AddressOf OnReplaceAll
                AddHandler pCloseButton.Clicked, AddressOf OnClose
                AddHandler pQuickFindButton.Clicked, AddressOf OnQuickFindClicked
                AddHandler pCancelButton.Clicked, AddressOf OnCancelOptimized
                
                ' Options events
                AddHandler pCaseSensitiveCheck.Toggled, AddressOf OnOptionsChanged
                AddHandler pWholeWordCheck.Toggled, AddressOf OnOptionsChanged
                AddHandler pRegexCheck.Toggled, AddressOf OnOptionsChanged
                
                ' Scope toggle events - pInFileRadio/pInProjectRadio are manually kept mutually
                ' exclusive in OnScopeToggled since CustomDrawCheckBox has no radio-group concept
                AddHandler pInFileRadio.Toggled, AddressOf OnScopeToggled
                AddHandler pInProjectRadio.Toggled, AddressOf OnScopeToggled
                
                ' Results grid events - single click/keyboard selection navigates directly
                ' using the row's own FindResult (DataGridRow.Tag), so no separate
                ' double-click handler is needed the way the old TreeView's RowActivated was
                AddHandler pResultsGrid.SelectionChanged, AddressOf OnResultsSelectionChanged
                
            Catch ex As Exception
                Console.WriteLine($"ConnectEvents error: {ex.Message}")
            End Try
        End Sub

        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handles the Activated event (Enter key) for the find entry
        ''' </summary>
        Private Sub OnFindEntryActivated(vSender As Object, vArgs As EventArgs)
            Try
                Console.WriteLine("OnFindEntryActivated: Enter pressed via Activated event!")
                
                ' Execute Find All if we have text
                If Not String.IsNullOrEmpty(pFindEntry.Text) Then
                    OnFind(Nothing, Nothing)
                Else
                    pStatusLabel.Text = "Please enter search text"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnFindEntryActivated error: {ex.Message}")
            End Try
        End Sub
        

        





        Private Sub OnClose(vSender As Object, vE As EventArgs)
            RaiseEvent CloseRequested()
        End Sub

        ' ===== Search Implementation =====

        ''' <summary>
        ''' Executes search without caching - always fresh from source
        ''' </summary>
        ''' <param name="vOnComplete">
        ''' Optional callback invoked once results are ready and populated. For current-file
        ''' scope this happens synchronously before ExecuteSearch returns; for project scope
        ''' the underlying search runs on background tasks, so the callback is invoked later
        ''' on the GTK main thread once those tasks finish. Callers that need to inspect
        ''' pSearchResults immediately after searching (e.g. F3/Shift+F3 navigation) MUST use
        ''' this callback rather than assuming ExecuteSearch is synchronous.
        ''' </param>
        Private Sub ExecuteSearch(Optional vOnComplete As System.Action = Nothing)
            Try
                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    pStatusLabel.Text = "Please enter search Text"
                    Return
                End If

                ' Save search options (but NOT results)
                pLastSearchOptions = New SearchOptions With {
                    .SearchText = pFindEntry.Text,
                    .ReplaceText = pReplaceEntry.Text,
                    .MatchCase = pCaseSensitiveCheck.Active,
                    .WholeWord = pWholeWordCheck.Active,
                    .UseRegex = pRegexCheck.Active,
                    .Scope = If(pInProjectRadio.Active, SearchScope.eProject, SearchScope.eCurrentFile)
                }

                ' ALWAYS clear previous results - no caching
                pResultsGrid.ClearRows()
                pSearchResults.Clear()
                pCurrentMatches = Nothing
                ' Don't reset pCurrentMatchIndex here - callers that want a fresh Find All
                ' (OnFind) reset it explicitly before calling ExecuteSearch; callers that want
                ' to advance through results (OnFindNext/OnFindPrevious) rely on it surviving.

                ' Perform fresh search. Current-file search is synchronous, so vOnComplete can
                ' be invoked immediately; project search may finish on a background task, so
                ' the callback is threaded through and invoked there instead.
                If pInFileRadio.Active Then
                    SearchInCurrentFile()
                    UpdateSearchHighlights()
                    UpdateButtonStates()
                    vOnComplete?.Invoke()
                Else
                    ' Use optimized in-memory search if available
                    If pProjectManager IsNot Nothing Then
                        SearchInProjectOptimized(Sub()
                            UpdateSearchHighlights()
                            vOnComplete?.Invoke()
                        End Sub)
                    Else
                        SearchInProject(Sub()
                            UpdateSearchHighlights()
                            vOnComplete?.Invoke()
                        End Sub)
                    End If
                    UpdateButtonStates()
                End If

            Catch ex As Exception
                Console.WriteLine($"ExecuteSearch error: {ex.Message}")
                pStatusLabel.Text = "Search error: " & ex.Message
            End Try
        End Sub
        
        Private Sub SearchInCurrentFile()
            Try
                Dim lTab As TabInfo = GetCurrentTab()
                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then
                    pStatusLabel.Text = "No file open to search"
                    Return
                End If
                
                ' Find all matches
                pCurrentMatches = New List(Of EditorPosition)(
                    lTab.Editor.Find(pLastSearchOptions.SearchText, pLastSearchOptions.MatchCase, 
                                   pLastSearchOptions.WholeWord, pLastSearchOptions.UseRegex))
                
                If pCurrentMatches.Count = 0 Then
                    pStatusLabel.Text = "No matches found"
                    Return
                End If
                
                ' Build FindResult list
                pSearchResults.Clear()
                for each lMatch in pCurrentMatches
                    Dim lLineText As String = lTab.Editor.GetLineText(lMatch.Line)
                    Dim lResult As New FindResult with {
                        .FilePath = lTab.FilePath,
                        .LineNumber = lMatch.Line + 1,  ' Convert to 1-based
                        .ColumnNumber = lMatch.Column + 1,
                        .LineText = lLineText.Trim(),
                        .MatchLength = pLastSearchOptions.SearchText.Length,
                        .MatchText = pLastSearchOptions.SearchText
                    }
                    
                    pSearchResults.Add(lResult)
                Next
                
                ' Use the new sortable population method
                PopulateSortableResults(pSearchResults)
                
                pStatusLabel.Text = $"Found {pCurrentMatches.Count} match(es) in current file"
                
            Catch ex As Exception
                Console.WriteLine($"SearchInCurrentFile error: {ex.Message}")
                pStatusLabel.Text = "Search error: " & ex.Message
            End Try
        End Sub
        
        Private Sub SearchInProject(Optional vOnComplete As System.Action = Nothing)
            Try
                If String.IsNullOrEmpty(pProjectRoot) Then
                    pStatusLabel.Text = "No project open"
                    vOnComplete?.Invoke()
                    Return
                End If
                
                ' Show progress
                pProgressBar.Visible = True
                pCancelButton.Visible = True
                pIsSearching = True
                
                ' Clear previous results
                pSearchResults.Clear()
                
                ' Get all project files
                Dim lFiles As New List(Of String)()
                GetProjectFiles(pProjectRoot, lFiles)
                
                Dim lTotalMatches As Integer = 0
                Dim lFilesSearched As Integer = 0
                
                for each lFile in lFiles
                    If Not pIsSearching Then Exit for
                    
                    ' Update progress
                    pProgressBar.Fraction = CDbl(lFilesSearched) / CDbl(lFiles.Count)
                    pStatusLabel.Text = $"Searching {System.IO.Path.GetFileName(lFile)}..."
                    
                    ' Process pending events
                    While Application.EventsPending()
                        Application.RunIteration()
                    End While
                    
                    ' Search file
                    Dim lMatches As Integer = SearchFile(lFile)
                    lTotalMatches += lMatches
                    lFilesSearched += 1
                Next
                
                ' Populate results with sorting support
                PopulateSortableResults(pSearchResults)
                
                ' Hide progress
                pProgressBar.Visible = False
                pCancelButton.Visible = False
                pIsSearching = False
                
                pStatusLabel.Text = $"Found {lTotalMatches} match(es) in {lFilesSearched} file(s)"

                vOnComplete?.Invoke()

            Catch ex As Exception
                Console.WriteLine($"SearchInProject error: {ex.Message}")
                pStatusLabel.Text = "Search error: " & ex.Message
                pProgressBar.Visible = False
                pCancelButton.Visible = False
                pIsSearching = False
                vOnComplete?.Invoke()
            End Try
        End Sub

        Private Function SearchFile(vFilePath As String) As Integer
            Try
                ' Read file content
                Dim lContent As String = System.IO.File.ReadAllText(vFilePath)
                Dim lLines() As String = lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                
                Dim lMatchCount As Integer = 0
                
                ' Search each line
                for lLineIndex As Integer = 0 To lLines.Length - 1
                    Dim lLine As String = lLines(lLineIndex)
                    Dim lMatches As List(Of Integer) = FindMatchesInLine(lLine, pLastSearchOptions)
                    
                    for each lColumn in lMatches
                        Dim lResult As New FindResult with {
                            .FilePath = vFilePath,
                            .LineNumber = lLineIndex + 1,
                            .ColumnNumber = lColumn + 1,
                            .LineText = lLine.Trim(),
                            .MatchLength = pLastSearchOptions.SearchText.Length,
                            .MatchText = pLastSearchOptions.SearchText
                        }
                        
                        pSearchResults.Add(lResult)
                        lMatchCount += 1
                    Next
                Next
                
                Return lMatchCount
                
            Catch ex As Exception
                Console.WriteLine($"SearchFile error in {vFilePath}: {ex.Message}")
                Return 0
            End Try
        End Function
        
        Private Function FindMatchesInLine(vLine As String, vOptions As SearchOptions) As List(Of Integer)
            Dim lMatches As New List(Of Integer)()
            
            Try
                If vOptions.UseRegex Then
                    ' Regex search
                    Dim lRegex As New Regex(vOptions.SearchText, 
                        If(vOptions.MatchCase, RegexOptions.None, RegexOptions.IgnoreCase))
                    
                    for each lMatch As Match in lRegex.Matches(vLine)
                        lMatches.Add(lMatch.Index)
                    Next
                Else
                    ' Plain text search
                    Dim lComparison As StringComparison = If(vOptions.MatchCase, 
                        StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)
                    
                    Dim lIndex As Integer = 0
                    While lIndex >= 0
                        lIndex = vLine.IndexOf(vOptions.SearchText, lIndex, lComparison)
                        If lIndex >= 0 Then
                            If Not vOptions.WholeWord OrElse IsWholeWordMatch(vLine, lIndex, vOptions.SearchText) Then
                                lMatches.Add(lIndex)
                            End If
                            lIndex += 1
                        End If
                    End While
                End If
                
            Catch ex As Exception
                Console.WriteLine($"FindMatchesInLine error: {ex.Message}")
            End Try
            
            Return lMatches
        End Function
        
        Private Function IsWholeWordMatch(vLine As String, vIndex As Integer, vSearchText As String) As Boolean
            ' Check if match at index is a whole word
            Dim lStartOk As Boolean = vIndex = 0 OrElse Not Char.IsLetterOrDigit(vLine(vIndex - 1))
            Dim lEndIndex As Integer = vIndex + vSearchText.Length
            Dim lEndOk As Boolean = lEndIndex >= vLine.Length OrElse Not Char.IsLetterOrDigit(vLine(lEndIndex))
            
            Return lStartOk AndAlso lEndOk
        End Function
        
        Private Function IsWholeWordMatch(vText As String, vSearchText As String, vCaseSensitive As Boolean) As Boolean
            ' Check if entire text is a whole word match
            If vCaseSensitive Then
                Return vText = vSearchText
            Else
                Return String.Equals(vText, vSearchText, StringComparison.OrdinalIgnoreCase)
            End If
        End Function

        ' ===== Replace Implementation =====

        ''' <summary>
        ''' Replaces all matches in the current file's open editor tab, which routes through
        ''' IEditor.ReplaceAll (single undo group, in-memory only - user saves manually)
        ''' </summary>
        Private Sub ReplaceAllInCurrentFile()
            Try
                Dim lTab As TabInfo = GetCurrentTab()
                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then
                    UpdateStatus("No file open")
                    Return
                End If

                lTab.Editor.ReplaceAll(pFindEntry.Text, pReplaceEntry.Text,
                                       pCaseSensitiveCheck.Active, pWholeWordCheck.Active, pRegexCheck.Active)

                UpdateStatus("Replace All complete")

                ' Refresh results to reflect the post-replace state
                ExecuteSearch()

            Catch ex As Exception
                Console.WriteLine($"ReplaceAllInCurrentFile error: {ex.Message}")
                UpdateStatus("Replace error: " & ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Replaces all matches across the project. Runs a fresh search first so the file
        ''' list and match counts are current, then for each file: if it's open in an editor
        ''' tab, replaces via IEditor.ReplaceAll (in-memory, undo available, user saves
        ''' manually); otherwise replaces the in-memory SourceFileInfo (or raw disk content
        ''' if no ProjectManager is available) and saves it immediately, since a file with no
        ''' open tab has no other save path and would otherwise become an invisible,
        ''' unsaved-and-unsavable in-memory change.
        ''' </summary>
        Private Sub ReplaceAllInProject()
            Try
                If pProjectManager Is Nothing AndAlso String.IsNullOrEmpty(pProjectRoot) Then
                    UpdateStatus("No project open")
                    Return
                End If

                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    UpdateStatus("Please enter search text")
                    Return
                End If

                Dim lDialog As New MessageDialog(
                    CType(Toplevel, Window),
                    DialogFlags.Modal,
                    MessageType.Warning,
                    ButtonsType.YesNo,
                    $"Replace all occurrences of '{pFindEntry.Text}' with '{pReplaceEntry.Text}' in the entire project?" & Environment.NewLine & Environment.NewLine &
                    "Files open in an editor tab will be updated in memory (undo available, save manually)." & Environment.NewLine &
                    "Files not currently open will be updated and saved to disk immediately."
                )

                Dim lResponse As Integer = lDialog.Run()
                lDialog.Destroy()
                If lResponse <> CInt(ResponseType.Yes) Then Return

                ' Run a fresh search first so we replace exactly what's currently found,
                ' and route the completion through ExecuteSearch's callback so this works
                ' correctly whether project search resolves synchronously or in the
                ' background (see ExecuteSearch's vOnComplete remarks).
                ExecuteSearch(AddressOf CompleteReplaceAllInProject)

            Catch ex As Exception
                Console.WriteLine($"ReplaceAllInProject error: {ex.Message}")
                UpdateStatus("Replace error: " & ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Performs the actual per-file replacement once a fresh project search has
        ''' completed and pSearchResults reflects the current match set
        ''' </summary>
        Private Sub CompleteReplaceAllInProject()
            Try
                If pSearchResults.Count = 0 Then
                    UpdateStatus("No matches found")
                    Return
                End If

                Dim lOpenTabsArgs As New OpenTabsEventArgs()
                RaiseEvent OnRequestOpenTabs(Me, lOpenTabsArgs)
                Dim lOpenTabsByPath As New Dictionary(Of String, TabInfo)(StringComparer.OrdinalIgnoreCase)
                If lOpenTabsArgs.OpenTabs IsNot Nothing Then
                    For Each lT In lOpenTabsArgs.OpenTabs
                        If Not String.IsNullOrEmpty(lT.FilePath) Then lOpenTabsByPath(lT.FilePath) = lT
                    Next
                End If

                Dim lFilePaths As List(Of String) = pSearchResults.Select(Function(r) r.FilePath).Distinct().ToList()

                Dim lTotalReplaced As Integer = 0
                Dim lFilesModified As Integer = 0
                Dim lFailedSaves As New List(Of String)

                pProgressBar.Visible = True
                pIsSearching = True

                Try
                    For Each lFilePath In lFilePaths
                        If Not pIsSearching Then Exit For

                        pStatusLabel.Text = $"Replacing in {System.IO.Path.GetFileName(lFilePath)}..."
                        While Application.EventsPending()
                            Application.RunIteration()
                        End While

                        Dim lReplaced As Integer = 0

                        If lOpenTabsByPath.ContainsKey(lFilePath) AndAlso lOpenTabsByPath(lFilePath).Editor IsNot Nothing Then
                            ' File has an open tab - replace through the editor so undo,
                            ' redraw, and the modified-tab indicator all work normally
                            Dim lEditor As IEditor = lOpenTabsByPath(lFilePath).Editor
                            lReplaced = pSearchResults.Where(Function(r) r.FilePath = lFilePath).Count()
                            lEditor.ReplaceAll(pFindEntry.Text, pReplaceEntry.Text,
                                               pCaseSensitiveCheck.Active, pWholeWordCheck.Active, pRegexCheck.Active)

                        ElseIf pProjectManager IsNot Nothing Then
                            Dim lSourceFile As SourceFileInfo = pProjectManager.GetSourceFileInfo(lFilePath)
                            If lSourceFile IsNot Nothing AndAlso lSourceFile.TextLines IsNot Nothing Then
                                lReplaced = ReplaceAllInSourceFileInfo(lSourceFile, lFilePath, lFailedSaves)
                            Else
                                lReplaced = ReplaceInFileOnDisk(lFilePath)
                            End If
                        Else
                            lReplaced = ReplaceInFileOnDisk(lFilePath)
                        End If

                        If lReplaced > 0 Then
                            lTotalReplaced += lReplaced
                            lFilesModified += 1
                        End If
                    Next
                Finally
                    pProgressBar.Visible = False
                    pIsSearching = False
                End Try

                pStatusLabel.Text = $"Replaced {lTotalReplaced} occurrence(s) in {lFilesModified} file(s)"

                If lFailedSaves.Count > 0 Then
                    Dim lFailedList As String = String.Join(Environment.NewLine, lFailedSaves.Select(Function(p) System.IO.Path.GetFileName(p)))
                    Dim lErrorDialog As New MessageDialog(
                        CType(Toplevel, Window),
                        DialogFlags.Modal,
                        MessageType.Error,
                        ButtonsType.Ok,
                        $"{lFailedSaves.Count} file(s) were replaced in memory but could not be saved to disk:{Environment.NewLine}{lFailedList}")
                    lErrorDialog.Run()
                    lErrorDialog.Destroy()
                End If

                ' Refresh results to reflect the post-replace state
                ExecuteSearch()

            Catch ex As Exception
                Console.WriteLine($"CompleteReplaceAllInProject error: {ex.Message}")
                pStatusLabel.Text = "Replace error: " & ex.Message
                pProgressBar.Visible = False
                pIsSearching = False
            End Try
        End Sub

        ''' <summary>
        ''' Replaces all matches directly in a SourceFileInfo's in-memory TextLines (used for
        ''' project files that aren't open in an editor tab) and saves the result to disk
        ''' immediately, since an unopened file has no other save path
        ''' </summary>
        ''' <returns>Number of occurrences replaced</returns>
        Private Function ReplaceAllInSourceFileInfo(vSourceFile As SourceFileInfo, vFilePath As String, vFailedSaves As List(Of String)) As Integer
            Try
                Dim lOriginalContent As String = String.Join(Environment.NewLine, vSourceFile.TextLines)
                Dim lReplaceCount As Integer = 0
                Dim lNewContent As String = ApplyReplaceAll(lOriginalContent, pLastSearchOptions, pReplaceEntry.Text, lReplaceCount)

                If lReplaceCount = 0 OrElse lNewContent = lOriginalContent Then Return 0

                Dim lNewLines() As String = lNewContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                vSourceFile.TextLines.Clear()
                vSourceFile.TextLines.AddRange(lNewLines)
                vSourceFile.IsModified = True
                vSourceFile.NotifyRenderingChanged(0, Math.Max(0, vSourceFile.TextLines.Count - 1))

                If Not vSourceFile.SaveContent() Then
                    Console.WriteLine($"ReplaceAllInSourceFileInfo: failed To save {vFilePath}")
                    vFailedSaves.Add(vFilePath)
                End If

                Return lReplaceCount

            Catch ex As Exception
                Console.WriteLine($"ReplaceAllInSourceFileInfo error for {vFilePath}: {ex.Message}")
                Return 0
            End Try
        End Function

        ''' <summary>
        ''' Replaces all matches directly on disk - only used as a fallback when no
        ''' ProjectManager is available (no project loaded, ad hoc folder search)
        ''' </summary>
        ''' <returns>Number of occurrences replaced</returns>
        Private Function ReplaceInFileOnDisk(vFilePath As String) As Integer
            Try
                Dim lOriginalContent As String = File.ReadAllText(vFilePath)
                Dim lReplaceCount As Integer = 0
                Dim lNewContent As String = ApplyReplaceAll(lOriginalContent, pLastSearchOptions, pReplaceEntry.Text, lReplaceCount)

                If lReplaceCount > 0 AndAlso lNewContent <> lOriginalContent Then
                    File.WriteAllText(vFilePath, lNewContent)
                End If

                Return lReplaceCount

            Catch ex As Exception
                Console.WriteLine($"ReplaceInFileOnDisk error in {vFilePath}: {ex.Message}")
                Return 0
            End Try
        End Function

        ''' <summary>
        ''' Performs a whole-content find/replace honoring the given options (plain,
        ''' whole-word, or regex), mirroring the matching rules used elsewhere in this class
        ''' </summary>
        ''' <param name="vReplaceCount">Receives the number of occurrences replaced</param>
        Private Function ApplyReplaceAll(vContent As String, vOptions As SearchOptions, vReplaceText As String, ByRef vReplaceCount As Integer) As String
            vReplaceCount = 0
            Try
                Dim lContent As String = vContent

                If vOptions.UseRegex Then
                    Dim lRegex As New Regex(vOptions.SearchText,
                        If(vOptions.MatchCase, RegexOptions.None, RegexOptions.IgnoreCase))
                    vReplaceCount = lRegex.Matches(vContent).Count
                    If vReplaceCount > 0 Then
                        lContent = lRegex.Replace(vContent, vReplaceText)
                    End If

                ElseIf vOptions.WholeWord Then
                    Dim lPattern As String = "\b" & Regex.Escape(vOptions.SearchText) & "\b"
                    Dim lRegex As New Regex(lPattern,
                        If(vOptions.MatchCase, RegexOptions.None, RegexOptions.IgnoreCase))
                    vReplaceCount = lRegex.Matches(vContent).Count
                    If vReplaceCount > 0 Then
                        ' Whole Word is a plain-text search modifier, not regex mode - the
                        ' MatchEvaluator overload (not the replacement-PATTERN string overload)
                        ' inserts vReplaceText literally. Regex.Replace(input, pattern,
                        ' replacementString) treats $ in the replacement as a substitution
                        ' token ($0/$$/etc), which silently mangled any replacement text
                        ' containing a literal $ (confirmed: replacing with "cost=$0 total"
                        ' produced "cost=<match> total" instead of the literal text).
                        lContent = lRegex.Replace(vContent, Function(m) vReplaceText)
                    End If

                Else
                    Dim lComparison As StringComparison = If(vOptions.MatchCase,
                        StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)

                    Dim lIndex As Integer = 0
                    While lIndex >= 0
                        lIndex = vContent.IndexOf(vOptions.SearchText, lIndex, lComparison)
                        If lIndex >= 0 Then
                            vReplaceCount += 1
                            lIndex += vOptions.SearchText.Length
                        End If
                    End While

                    If vReplaceCount > 0 Then
                        If vOptions.MatchCase Then
                            lContent = vContent.Replace(vOptions.SearchText, vReplaceText)
                        Else
                            Dim lRegex As New Regex(Regex.Escape(vOptions.SearchText), RegexOptions.IgnoreCase)
                            ' Same literal-replacement fix as the WholeWord branch above - this
                            ' is still plain-text (case-insensitive) mode, not regex mode
                            lContent = lRegex.Replace(vContent, Function(m) vReplaceText)
                        End If
                    End If
                End If

                Return lContent

            Catch ex As Exception
                Console.WriteLine($"ApplyReplaceAll error: {ex.Message}")
                vReplaceCount = 0
                Return vContent
            End Try
        End Function
        
        ' ===== Helper Methods =====
        
        Private Function GetCurrentTab() As TabInfo
            Try
                Dim lTabArgs As New TabInfoEventArgs()
                RaiseEvent OnRequestCurrentTab(lTabArgs)
                Return lTabArgs.TabInfo
            Catch ex As Exception
                Console.WriteLine($"GetCurrentTab error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Highlights every current-search match that falls in the active tab's file (for
        ''' either scope - project-wide results are filtered down to that one file), or
        ''' clears highlighting if there's no active tab or no matches in it
        ''' </summary>
        Private Sub UpdateSearchHighlights()
            Try
                Dim lTab As TabInfo = GetCurrentTab()
                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then Return

                Dim lFileMatches As List(Of EditorPosition) = pSearchResults.
                    Where(Function(r) r.FilePath = lTab.FilePath).
                    Select(Function(r) New EditorPosition(r.LineNumber - 1, r.ColumnNumber - 1)).
                    ToList()

                If lFileMatches.Count > 0 Then
                    lTab.Editor.HighlightSearchMatches(lFileMatches, pLastSearchOptions.SearchText.Length)
                Else
                    lTab.Editor.ClearSearchHighlights()
                End If

            Catch ex As Exception
                Console.WriteLine($"UpdateSearchHighlights error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Navigates to a search result by index via the ResultSelected event, which
        ''' MainWindow handles by opening/switching to the file and moving the cursor.
        ''' Works uniformly for both current-file and project-wide search results.
        ''' </summary>
        ''' <param name="vIndex">Index into pSearchResults to navigate to</param>
        Private Sub NavigateToSearchResult(vIndex As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pSearchResults.Count Then Return
                Dim lResult As FindResult = pSearchResults(vIndex)
                RaiseEvent ResultSelected(lResult.FilePath, lResult.LineNumber, lResult.ColumnNumber)
            Catch ex As Exception
                Console.WriteLine($"NavigateToSearchResult error: {ex.Message}")
            End Try
        End Sub

        Private Sub GetProjectFiles(vPath As String, vFiles As List(Of String))
            Try
                ' ProjectFileScanner already skips bin/obj/.git/.claude/etc. during the walk
                vFiles.AddRange(ProjectFileScanner.GetVBFiles(vPath))

            Catch ex As Exception
                Console.WriteLine($"GetProjectFiles error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        Public Sub SetProjectRoot(vPath As String)
            pProjectRoot = vPath
        End Sub
        
        Public Sub FocusSearchEntry()
            pFindEntry.GrabFocus()
            pFindEntry.InnerEntry.SelectRegion(0, -1)
        End Sub
        
        Public Sub SetSearchText(vText As String)
            pFindEntry.Text = vText
        End Sub
        
        Public Sub SetReplaceText(vText As String)
            pReplaceEntry.Text = vText
        End Sub
        
        Public Sub SetOptions(vCaseSensitive As Boolean, vWholeWord As Boolean, vUseRegex As Boolean)
            pCaseSensitiveCheck.Active = vCaseSensitive
            pWholeWordCheck.Active = vWholeWord
            pRegexCheck.Active = vUseRegex
        End Sub
        
        Public Sub Clear()
            pResultsGrid.ClearRows()
            pSearchResults.Clear()
            pCurrentMatches = Nothing
            pCurrentMatchIndex = -1
            pStatusLabel.Text = "Ready"
            GetCurrentTab()?.Editor?.ClearSearchHighlights()
        End Sub
        
        Public Function HasSearchText() As Boolean
            Return Not String.IsNullOrEmpty(pFindEntry.Text)
        End Function
        
        Public Sub FindNext()
            OnFindNext(Nothing, Nothing)
        End Sub
        
        Public Sub FindPrevious()
            OnFindPrevious(Nothing, Nothing)
        End Sub
        
        Public Sub FocusReplaceEntry()
            pReplaceEntry.GrabFocus()
            pReplaceEntry.InnerEntry.SelectRegion(0, -1)
        End Sub
        
        Public ReadOnly Property IsSearching As Boolean
            Get
                Return pIsSearching
            End Get
        End Property

        ' Replace: SimpleIDE.Widgets.FindReplacePanel.OnFind
        Public Sub OnFind(vSender As Object, vE As EventArgs)
            Try
                ' Fresh Find All - start browsing from the first match again
                pCurrentMatchIndex = -1
                ExecuteSearch()

            Catch ex As Exception
                Console.WriteLine($"OnFind error: {ex.Message}")
                pStatusLabel.Text = "Search error: " & ex.Message
            End Try
        End Sub

        ''' <summary>
        ''' Focuses the search entry without selecting its contents
        ''' </summary>
        Public Sub FocusSearchEntryNoSelect()
            Try
                pFindEntry.GrabFocus()
                ' Move cursor to end of text without selecting
                pFindEntry.InnerEntry.Position = pFindEntry.Text.Length
            Catch ex As Exception
                Console.WriteLine($"FocusSearchEntryNoSelect error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the search scope to either current file or entire project
        ''' </summary>
        ''' <param name="vScope">The search scope to set (eCurrentFile or eProject)</param>
        Public Sub SetSearchScope(vScope As SearchScope)
            Try
                Select Case vScope
                    Case SearchScope.eCurrentFile
                        Console.WriteLine("SetSearchScope: Setting Scope To Current File")
                        pInFileRadio.Active = True
                        pInProjectRadio.Active = False
                        
                    Case SearchScope.eProject
                        Console.WriteLine("SetSearchScope: Setting Scope To Entire Project")
                        pInProjectRadio.Active = True
                        pInFileRadio.Active = False
                        
                    Case Else
                        Console.WriteLine($"SetSearchScope: Unsupported Scope {vScope}, defaulting To Current File")
                        pInFileRadio.Active = True
                        pInProjectRadio.Active = False
                End Select
                
                ' The OnScopeChanged event handler will update the status label automatically
                
            Catch ex As Exception
                Console.WriteLine($"SetSearchScope error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles ESC key press for the FindReplacePanel
        ''' </summary>
        ''' <returns>True if handled internally, False to let parent handle it</returns>
        Public Function HandleEscapeKey() As Boolean
            Try
                ' If search entry has focus and has selection, clear selection first
                If pFindEntry IsNot Nothing AndAlso pFindEntry.HasFocus Then
                    Dim lBounds As Integer() = {0, 0}
                    If pFindEntry.InnerEntry.GetSelectionBounds(lBounds(0), lBounds(1)) Then
                        ' Clear selection
                        pFindEntry.InnerEntry.SelectRegion(0, 0)
                        Return True ' Handled internally
                    End If
                End If
                
                ' If replace entry has focus and has selection, clear selection first
                If pReplaceEntry IsNot Nothing AndAlso pReplaceEntry.HasFocus Then
                    Dim lBounds As Integer() = {0, 0}
                    If pReplaceEntry.InnerEntry.GetSelectionBounds(lBounds(0), lBounds(1)) Then
                        ' Clear selection
                        pReplaceEntry.InnerEntry.SelectRegion(0, 0)
                        Return True ' Handled internally
                    End If
                End If
                
                ' Let parent handle the ESC to hide panel
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"HandleEscapeKey error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Pre-fills the search text from the active editor's selection or word at cursor -
        ''' the same source Ctrl+F's ShowFindPanel uses - and runs Find All if there was a
        ''' selection to search for. Leaves the current scope selection alone rather than
        ''' forcing it to Entire Project.
        ''' </summary>
        Private Sub PerformQuickFind()
            Try
                Dim lTab As TabInfo = GetCurrentTab()
                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then
                    FocusSearchEntry()
                    Return
                End If

                Dim lEditor As IEditor = lTab.Editor
                Dim lHasSelection As Boolean = False
                Dim lSearchText As String = ""

                If lEditor.HasSelection Then
                    Dim lSelectedText As String = lEditor.SelectedText
                    ' Only use if it's a single line
                    If Not String.IsNullOrEmpty(lSelectedText) AndAlso
                       Not lSelectedText.Contains(vbLf) AndAlso Not lSelectedText.Contains(vbCr) Then
                        lHasSelection = True
                        lSearchText = lSelectedText
                    End If
                Else
                    lSearchText = lEditor.GetWordAtCursor()
                End If

                If String.IsNullOrEmpty(lSearchText) Then
                    FocusSearchEntry()
                    Return
                End If

                SetSearchText(lSearchText)

                If lHasSelection Then
                    FocusSearchEntryNoSelect()
                    OnFind(Nothing, Nothing)
                Else
                    ' Word at cursor - prefill but let the user confirm before searching
                    FocusSearchEntryNoSelect()
                End If

            Catch ex As Exception
                Console.WriteLine($"PerformQuickFind error: {ex.Message}")
            End Try
        End Sub

        
    End Class

End Namespace
