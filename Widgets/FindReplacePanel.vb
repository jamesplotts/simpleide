' FindReplacePanel.vb - Complete Find/Replace panel implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Namespace Widgets
    Public Class FindReplacePanel
        Inherits Box

        ' UI Controls
        Private pFindEntry As Entry
        Private pReplaceEntry As Entry
        Private pFindButton As Button
        Private pReplaceButton As Button
        Private pReplaceAllButton As Button
        Private pFindNextButton As Button
        Private pFindPreviousButton As Button
        Private pCloseButton As Button
        Private pRefreshButton As Button
        Private pCaseSensitiveCheck As CheckButton
        Private pWholeWordCheck As CheckButton
        Private pRegexCheck As CheckButton
        Private pInFileRadio As RadioButton
        Private pInProjectRadio As RadioButton
        Private pStatusLabel As Label
        Private pProgressBar As ProgressBar
        Private pCancelButton As Button
        Private pResultsView As TreeView
        Private pResultsStore As ListStore
        Private pCurrentTab As TabInfo
        
        ' Search state
        Private pProjectRoot As String
        Private pSearchResults As New List(Of FindResult)()
        Private pCurrentResultIndex As Integer = -1
        Private pIsSearching As Boolean = False
        Private pLastSearchOptions As SearchOptions
        Private pModifiedFiles As New HashSet(Of String)
        
        ' Current file search state
        Private pCurrentEditor As IEditor
        Private pCurrentFilePath As String
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
            eOpenFiles 
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
                
                ' Results list with sortable columns
                Dim lResultsScroll As New ScrolledWindow()
                lResultsScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lResultsScroll.ShadowType = ShadowType.in
                
                ' Set a minimum height for the results scroll window
                lResultsScroll.HeightRequest = 200
                
                ' Use the new sortable results view
                pResultsView = CreateSortableResultsView()
                lResultsScroll.Add(pResultsView)
                PackStart(lResultsScroll, True, True, 0)
                
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
                    AddHandler pFindEntry.KeyPressEvent, AddressOf OnFindPanelKeyPress
                End If
                
                ' Connect ESC handler to replace entry
                If pReplaceEntry IsNot Nothing Then
                    AddHandler pReplaceEntry.KeyPressEvent, AddressOf OnFindPanelKeyPress
                End If
                
                ' Connect ESC handler to results tree view
                If pResultsView IsNot Nothing Then
                    AddHandler pResultsView.KeyPressEvent, AddressOf OnFindPanelKeyPress
                End If
                
                Console.WriteLine("FindReplacePanel: ESC handling initialized")
                
            Catch ex As Exception
                Console.WriteLine($"InitializeEscapeHandling error: {ex.Message}")
            End Try
        End Sub


        Private Function CreateSearchControls() As Widget
            Dim lMainBox As New Box(Orientation.Vertical, 5)
            
            ' First row: Find entry and buttons
            Dim lFindBox As New Box(Orientation.Horizontal, 5)
            
            Dim lFindLabel As New Label("Find:")
            lFindLabel.SetSizeRequest(80, -1)
            lFindLabel.Xalign = 0
            lFindBox.PackStart(lFindLabel, False, False, 0)
            
            pFindEntry = New Entry()
            pFindEntry.PlaceholderText = "Enter search Text..."
            lFindBox.PackStart(pFindEntry, True, True, 0)
            
            pFindButton = New Button("Find All")
            pFindNextButton = New Button("Next")
            pFindPreviousButton = New Button("Previous")
            
            lFindBox.PackStart(pFindButton, False, False, 0)
            lFindBox.PackStart(pFindNextButton, False, False, 0)
            lFindBox.PackStart(pFindPreviousButton, False, False, 0)
            
            lMainBox.PackStart(lFindBox, False, False, 0)
            
            ' Second row: Replace entry and buttons
            Dim lReplaceBox As New Box(Orientation.Horizontal, 5)
            
            Dim lReplaceLabel As New Label("Replace:")
            lReplaceLabel.SetSizeRequest(80, -1)
            lReplaceLabel.Xalign = 0
            lReplaceBox.PackStart(lReplaceLabel, False, False, 0)
            
            pReplaceEntry = New Entry()
            pReplaceEntry.PlaceholderText = "Enter replacement Text..."
            lReplaceBox.PackStart(pReplaceEntry, True, True, 0)
            
            pReplaceButton = New Button("Replace")
            pReplaceAllButton = New Button("Replace All")
            pCancelButton = New Button("Cancel")
            pRefreshButton = New Button("Refresh")
            pCloseButton = New Button("Close")
            
            lReplaceBox.PackStart(pReplaceButton, False, False, 0)
            lReplaceBox.PackStart(pReplaceAllButton, False, False, 0)
            lReplaceBox.PackStart(pCancelButton, False, False, 0)
            lReplaceBox.PackStart(pRefreshButton, False, False, 0)
            lReplaceBox.PackStart(pCloseButton, False, False, 0)
            
            lMainBox.PackStart(lReplaceBox, False, False, 0)
            
            Return lMainBox
        End Function

        Private Function CreateOptionsControls() As Widget
            Dim lOptionsBox As New Box(Orientation.Horizontal, 10)
            
            ' Search options
            Dim lOptionsFrame As New Frame("Options")
            Dim lOptionsInnerBox As New Box(Orientation.Horizontal, 5)
            lOptionsInnerBox.MarginTop = 5
            lOptionsInnerBox.MarginBottom = 5
            lOptionsInnerBox.MarginStart = 5
            lOptionsInnerBox.MarginEnd = 5
            
            pCaseSensitiveCheck = New CheckButton("Case sensitive")
            pWholeWordCheck = New CheckButton("Whole word")
            pRegexCheck = New CheckButton("Use regex")
            
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
            
            pInFileRadio = New RadioButton("current file")
            pInProjectRadio = New RadioButton(pInFileRadio, "Entire project")
            pInFileRadio.Active = True
            
            lScopeInnerBox.PackStart(pInFileRadio, False, False, 0)
            lScopeInnerBox.PackStart(pInProjectRadio, False, False, 0)
            
            lScopeFrame.Add(lScopeInnerBox)
            lOptionsBox.PackStart(lScopeFrame, False, False, 0)
            
            Return lOptionsBox
        End Function

        ''' <summary>
        ''' Creates the tree view for displaying search results
        ''' </summary>
        ''' <returns>Configured TreeView widget for search results</returns>
        Private Function CreateResultsView() As TreeView
            Try
                ' Create tree view
                Dim lTreeView As New TreeView()
                
                ' Create model (FilePath, LineText, LineNumber, ColumnNumber, MatchText)
                pResultsStore = New ListStore(GetType(String), GetType(String), GetType(Integer), GetType(Integer), GetType(String))
                lTreeView.Model = pResultsStore
                
                ' File column - FIXED: Use proper GTK# 3 syntax
                Dim lFileRenderer As New CellRendererText()
                Dim lFileColumn As New TreeViewColumn()
                lFileColumn.Title = "File"
                lFileColumn.PackStart(lFileRenderer, True)
                lFileColumn.AddAttribute(lFileRenderer, "text", 0)  ' Use lowercase "text"
                lFileColumn.Resizable = True
                lFileColumn.MinWidth = 200
                lTreeView.AppendColumn(lFileColumn)
                
                ' Line column - FIXED: Use proper GTK# 3 syntax
                Dim lLineRenderer As New CellRendererText()
                Dim lLineColumn As New TreeViewColumn()
                lLineColumn.Title = "Line"
                lLineColumn.PackStart(lLineRenderer, False)
                lLineColumn.AddAttribute(lLineRenderer, "text", 2)  ' Use lowercase "text"
                lLineColumn.Resizable = True
                lLineColumn.MinWidth = 60
                lTreeView.AppendColumn(lLineColumn)
                
                ' Text column - FIXED: Use proper GTK# 3 syntax
                Dim lTextRenderer As New CellRendererText()
                Dim lTextColumn As New TreeViewColumn()
                lTextColumn.Title = "Text"
                lTextColumn.PackStart(lTextRenderer, True)
                lTextColumn.AddAttribute(lTextRenderer, "text", 1)  ' Use lowercase "text"
                lTextColumn.Resizable = True
                lTextColumn.MinWidth = 300
                lTreeView.AppendColumn(lTextColumn)
                
                ' Configure tree view
                lTreeView.EnableSearch = True
                lTreeView.SearchColumn = 1
                lTreeView.HeadersVisible = True
                lTreeView.EnableGridLines = TreeViewGridLines.Horizontal
                
                Return lTreeView
                
            Catch ex As Exception
                Console.WriteLine($"CreateResultsView error: {ex.Message}")
                Return New TreeView()
            End Try
        End Function
        
        ''' <summary>
        ''' Connects event handlers for the FindReplacePanel
        ''' </summary>
        Private Sub ConnectEvents()
            Try
                ' Entry events - Connect Activated for Enter key
                AddHandler pFindEntry.Activated, AddressOf OnFindEntryActivated
                AddHandler pFindEntry.KeyPressEvent, AddressOf OnFindEntryKeyPress
                AddHandler pReplaceEntry.KeyPressEvent, AddressOf OnReplaceEntryKeyPress
                AddHandler pReplaceEntry.Activated, AddressOf OnReplaceEntryActivated
                
                ' Entry change events for live updates
                AddHandler pFindEntry.Changed, AddressOf OnFindEntryChanged
                
                ' Button events - Updated to use optimized handlers
                AddHandler pFindButton.Clicked, AddressOf OnFind  ' Will now use ExecuteSearchOptimized
                AddHandler pFindNextButton.Clicked, AddressOf OnFindNext
                AddHandler pFindPreviousButton.Clicked, AddressOf OnFindPrevious
                AddHandler pReplaceButton.Clicked, AddressOf OnReplace
                AddHandler pReplaceAllButton.Clicked, AddressOf OnReplaceAll
                AddHandler pCancelButton.Clicked, AddressOf OnCancelOptimized  ' Use optimized cancel
                AddHandler pRefreshButton.Clicked, AddressOf OnRefresh
                AddHandler pCloseButton.Clicked, AddressOf OnClose
                
                ' Results selection - FIXED: Add both single-click and double-click handling
                ' Single-click selection
                AddHandler pResultsView.CursorChanged, AddressOf OnResultsCursorChanged
                ' Double-click or Enter activation
                AddHandler pResultsView.RowActivated, AddressOf OnResultActivated
                
                ' Radio button changes - ENABLE for auto-search on scope change
                AddHandler pInFileRadio.Toggled, AddressOf OnScopeChanged
                AddHandler pInProjectRadio.Toggled, AddressOf OnScopeChanged
                
                ' Options changes - auto-search when options change
                AddHandler pCaseSensitiveCheck.Toggled, AddressOf OnOptionsChanged
                AddHandler pWholeWordCheck.Toggled, AddressOf OnOptionsChanged
                AddHandler pRegexCheck.Toggled, AddressOf OnOptionsChanged
                
                ' Context menu and keyboard
                AddHandler pResultsView.ButtonPressEvent, AddressOf OnResultsButtonPress
                AddHandler pResultsView.KeyPressEvent, AddressOf OnResultsKeyPress
                
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
        
        Private Sub ExecuteSearch()
            Try
                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    pStatusLabel.Text = "Please enter search Text"
                    Return
                End If
                
                ' Save search options
                pLastSearchOptions = New SearchOptions with {
                    .SearchText = pFindEntry.Text,
                    .ReplaceText = pReplaceEntry.Text,
                    .MatchCase = pCaseSensitiveCheck.Active,
                    .WholeWord = pWholeWordCheck.Active,
                    .UseRegex = pRegexCheck.Active,
                    .Scope = If(pInProjectRadio.Active, SearchScope.eProject, SearchScope.eCurrentFile)
                }
                
                ' Clear previous results
                pResultsStore.Clear()
                pSearchResults.Clear()
                pCurrentMatches = Nothing
                pCurrentMatchIndex = -1
                
                If pInFileRadio.Active Then
                    SearchInCurrentFile()
                Else
                    SearchInProject()
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
                
                pCurrentEditor = lTab.Editor
                pCurrentFilePath = lTab.FilePath
                
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
        
        Private Sub SearchInProject()
            Try
                If String.IsNullOrEmpty(pProjectRoot) Then
                    pStatusLabel.Text = "No project open"
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
                
            Catch ex As Exception
                Console.WriteLine($"SearchInProject error: {ex.Message}")
                pStatusLabel.Text = "Search error: " & ex.Message
                pProgressBar.Visible = False
                pCancelButton.Visible = False
                pIsSearching = False
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
        Private Sub ReplaceAllInCurrentFile()
            Try
                Dim lTab As TabInfo = GetCurrentTab()
                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then
                    pStatusLabel.Text = "No file open"
                    Return
                End If
                
                ' Find all matches
                Dim lMatches As List(Of EditorPosition) = lTab.Editor.Find(
                    pLastSearchOptions.SearchText,
                    pLastSearchOptions.MatchCase,
                    pLastSearchOptions.WholeWord,
                    pLastSearchOptions.UseRegex
                ).ToList()
                
                If lMatches.Count = 0 Then
                    pStatusLabel.Text = "No matches found"
                    Return
                End If
                
                Try
                    lTab.Editor.BeginUpdate()
                    
                    ' Determine replacement text
                    Dim lReplaceText As String = pReplaceEntry.Text
                    
                    ' Replace from end to beginning to maintain positions
                    for i As Integer = lMatches.Count - 1 To 0 Step -1
                        Dim lPosition As EditorPosition = lMatches(i)
                        
                        ' Calculate end position
                        Dim lEndLine As Integer = lPosition.Line
                        Dim lEndColumn As Integer = lPosition.Column + pLastSearchOptions.SearchText.Length
                        
                        ' Handle multi-line matches for regex
                        If pLastSearchOptions.UseRegex Then
                            Dim lLineText As String = lTab.Editor.GetLineText(lPosition.Line)
                            Dim lRegex As New Regex(pLastSearchOptions.SearchText)
                            Dim lMatch As Match = lRegex.Match(lLineText, lPosition.Column)
                            If lMatch.Success Then
                                lEndColumn = lPosition.Column + lMatch.Length
                            End If
                        End If
                        
                        ' Replace text - using lPosition for start position
                        lTab.Editor.ReplaceText(lPosition,
                                                New EditorPosition(lEndLine, lEndColumn), 
                                                lReplaceText)
                    Next
                    
                    pStatusLabel.Text = $"Replaced {lMatches.Count} occurrence(s)"
                    
                    ' Clear search results as text has changed
                    pCurrentMatches = Nothing
                    pCurrentMatchIndex = -1
                    
                Finally
                    lTab.Editor.EndUpdate()
                End Try
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceAllInCurrentFile error: {ex.Message}")
                pStatusLabel.Text = "Replace error: " & ex.Message
            End Try
        End Sub
        
        Private Sub ReplaceAllInProject()
            Try
                If String.IsNullOrEmpty(pProjectRoot) Then
                    pStatusLabel.Text = "No project open"
                    Return
                End If
                
                ' Confirm with user
                Dim lDialog As New MessageDialog(
                    CType(Toplevel, Window),
                    DialogFlags.Modal,
                    MessageType.Warning,
                    ButtonsType.YesNo,
                    $"Replace all occurrences of '{pFindEntry.Text}' with '{pReplaceEntry.Text}' in the entire project?{Environment.NewLine}{Environment.NewLine}this action cannot be undone."
                )
                
                If lDialog.Run() <> CInt(ResponseType.Yes) Then
                    lDialog.Destroy()
                    Return
                End If
                lDialog.Destroy()
                
                ' Show progress
                pProgressBar.Visible = True
                pCancelButton.Visible = True
                pIsSearching = True
                
                ' Get all project files
                Dim lFiles As New List(Of String)()
                GetProjectFiles(pProjectRoot, lFiles)
                
                Dim lTotalReplaced As Integer = 0
                Dim lFilesModified As Integer = 0
                pModifiedFiles.Clear()
                
                For Each lFile In lFiles
                    If Not pIsSearching Then Exit For
                    
                    ' Update progress
                    pProgressBar.Fraction = CDbl(lFilesModified) / CDbl(lFiles.Count)
                    pStatusLabel.Text = $"Processing {System.IO.Path.GetFileName(lFile)}..."
                    
                    ' Process pending events
                    While Application.EventsPending()
                        Application.RunIteration()
                    End While
                    
                    ' Replace in file
                    Dim lReplaced As Integer = ReplaceInFile(lFile)
                    If lReplaced > 0 Then
                        lTotalReplaced += lReplaced
                        lFilesModified += 1
                        pModifiedFiles.Add(lFile)
                    End If
                Next
                
                ' Hide progress
                pProgressBar.Visible = False
                pCancelButton.Visible = False
                pIsSearching = False
                
                pStatusLabel.Text = $"Replaced {lTotalReplaced} occurrence(s) in {lFilesModified} file(s)"
                
                ' Refresh open tabs that were modified
                RefreshModifiedTabs()
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceAllInProject error: {ex.Message}")
                pStatusLabel.Text = "Replace error: " & ex.Message
                pProgressBar.Visible = False
                pCancelButton.Visible = False
                pIsSearching = False
            End Try
        End Sub
        
        Private Function ReplaceInFile(vFilePath As String) As Integer
            Try
                ' Read file content
                Dim lContent As String = File.ReadAllText(vFilePath)
                Dim lOriginalContent As String = lContent
                Dim lReplaceCount As Integer = 0
                
                If pLastSearchOptions.UseRegex Then
                    ' Regex replace
                    Dim lRegex As New Regex(pLastSearchOptions.SearchText,
                        If(pLastSearchOptions.MatchCase, RegexOptions.None, RegexOptions.IgnoreCase))
                    
                    lContent = lRegex.Replace(lContent, pReplaceEntry.Text)
                    lReplaceCount = lRegex.Matches(lOriginalContent).Count
                Else
                    ' Plain text replace
                    If pLastSearchOptions.WholeWord Then
                        ' Whole word replace using regex
                        Dim lPattern As String = "\b" & Regex.Escape(pLastSearchOptions.SearchText) & "\b"
                        Dim lRegex As New Regex(lPattern,
                            If(pLastSearchOptions.MatchCase, RegexOptions.None, RegexOptions.IgnoreCase))
                        
                        lContent = lRegex.Replace(lContent, pReplaceEntry.Text)
                        lReplaceCount = lRegex.Matches(lOriginalContent).Count
                    Else
                        ' Simple replace
                        Dim lComparison As StringComparison = If(pLastSearchOptions.MatchCase,
                            StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)
                        
                        ' Count occurrences
                        Dim lIndex As Integer = 0
                        While lIndex >= 0
                            lIndex = lContent.IndexOf(pLastSearchOptions.SearchText, lIndex, lComparison)
                            If lIndex >= 0 Then
                                lReplaceCount += 1
                                lIndex += pLastSearchOptions.SearchText.Length
                            End If
                        End While
                        
                        ' Perform replacement
                        If lReplaceCount > 0 Then
                            If pLastSearchOptions.MatchCase Then
                                lContent = lContent.Replace(pLastSearchOptions.SearchText, pReplaceEntry.Text)
                            Else
                                ' Case-insensitive replace
                                Dim lRegex As New Regex(Regex.Escape(pLastSearchOptions.SearchText), RegexOptions.IgnoreCase)
                                lContent = lRegex.Replace(lContent, pReplaceEntry.Text)
                            End If
                        End If
                    End If
                End If
                
                ' Write back if changed
                If lReplaceCount > 0 AndAlso lContent <> lOriginalContent Then
                    File.WriteAllText(vFilePath, lContent)
                End If
                
                Return lReplaceCount
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceInFile error in {vFilePath}: {ex.Message}")
                Return 0
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
        
        Private Sub NavigateToMatch(vIndex As Integer)
            Try
                If pCurrentMatches Is Nothing OrElse vIndex < 0 OrElse vIndex >= pCurrentMatches.Count Then
                    Return
                End If
                
                Dim lMatch As EditorPosition = pCurrentMatches(vIndex)
                
                ' Navigate to position
                pCurrentEditor.GoToPosition(New EditorPosition(lMatch.Line, lMatch.Column))
                
                ' Select the match
                Dim lEndColumn As Integer = lMatch.Column + pLastSearchOptions.SearchText.Length
                pCurrentEditor.SetSelection(New EditorPosition(lMatch.Line, lMatch.Column), New EditorPosition(lMatch.Line, lEndColumn))
                
                ' Update status
                pStatusLabel.Text = $"Match {vIndex + 1} Of {pCurrentMatches.Count}"
                
                ' Ensure editor has focus
                pCurrentEditor.Widget.GrabFocus()
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToMatch error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub GetProjectFiles(vPath As String, vFiles As List(Of String))
            Try
                ' Add VB files
                vFiles.AddRange(Directory.GetFiles(vPath, "*.vb", SearchOption.AllDirectories))
                
                ' Exclude bin and obj directories
                vFiles.RemoveAll(Function(f) f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}") OrElse
                                           f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}"))
                
            Catch ex As Exception
                Console.WriteLine($"GetProjectFiles error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub RefreshModifiedTabs()
            Try
                ' Get all open tabs
                Dim lOpenTabsArgs As New OpenTabsEventArgs()
                RaiseEvent OnRequestOpenTabs(Me, lOpenTabsArgs)
                
                If lOpenTabsArgs.OpenTabs IsNot Nothing Then
                    For Each lTab In lOpenTabsArgs.OpenTabs
                        If pModifiedFiles.Contains(lTab.FilePath) Then
                            ' Request file reload
                            RaiseEvent OpenFileRequested(lTab.FilePath)
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RefreshModifiedTabs error: {ex.Message}")
            End Try
        End Sub
        
        
        ' ===== Public Methods =====
        
        Public Sub SetProjectRoot(vPath As String)
            pProjectRoot = vPath
        End Sub
        
        Public Sub FocusSearchEntry()
            pFindEntry.GrabFocus()
            pFindEntry.SelectRegion(0, -1)
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
            pResultsStore.Clear()
            pSearchResults.Clear()
            pCurrentMatches = Nothing
            pCurrentMatchIndex = -1
            pStatusLabel.Text = "Ready"
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
            pReplaceEntry.SelectRegion(0, -1)
        End Sub
        
        Public ReadOnly Property IsSearching As Boolean
            Get
                Return pIsSearching
            End Get
        End Property

        ' Replace: SimpleIDE.Widgets.FindReplacePanel.OnFind
        Public Sub OnFind(vSender As Object, vE As EventArgs)
            Try
                ' Use optimized search
                ExecuteSearchOptimized()
                
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
                pFindEntry.Position = pFindEntry.Text.Length
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
                    If pFindEntry.GetSelectionBounds(lBounds(0), lBounds(1)) Then
                        ' Clear selection
                        pFindEntry.SelectRegion(0, 0)
                        Return True ' Handled internally
                    End If
                End If
                
                ' If replace entry has focus and has selection, clear selection first
                If pReplaceEntry IsNot Nothing AndAlso pReplaceEntry.HasFocus Then
                    Dim lBounds As Integer() = {0, 0}
                    If pReplaceEntry.GetSelectionBounds(lBounds(0), lBounds(1)) Then
                        ' Clear selection
                        pReplaceEntry.SelectRegion(0, 0)
                        Return True ' Handled internally
                    End If
                End If
                
                ' If we have search results and TreeView has focus, could clear selection
                If pResultsView IsNot Nothing AndAlso pResultsView.HasFocus Then
                    Dim lSelection As TreeSelection = pResultsView.Selection
                    If lSelection.CountSelectedRows() > 0 Then
                        ' Could clear selection if desired
                        ' lSelection.UnselectAll()
                        ' Return True
                    End If
                End If
                
                ' Let parent handle the ESC to hide panel
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"HandleEscapeKey error: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Class

End Namespace
