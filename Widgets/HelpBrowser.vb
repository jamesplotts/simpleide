' HelpBrowser.vb - Native GTK-based help browser for SimpleIDE
'
' This widget used to embed WebKit.WebView to render HTML help content. WebKitGTK's
' GTK3-compatible library (libwebkit2gtk-4.0/4.1) was removed from Debian's repositories
' starting with Debian 13 ("trixie") in favor of a GTK4-only rewrite (libwebkitgtk-6.0),
' which is ABI-incompatible with this GTK3 application - every WebKit.WebView construction
' throws System.DllNotFoundException on that OS ("libwebkit2gtk-4.0.so.37" not found), so
' the Help tab never rendered anything at all, on any page. This widget was rewritten to
' render its own generated content (Home, Keyboard Shortcuts, etc.) with native GTK
' widgets instead, and to open real external documentation links in the user's system
' default browser rather than trying to embed them - which also means every link now
' automatically follows the app's theme via the same global CSS provider every other
' native widget uses, instead of needing separate HTML dark-mode CSS injection.
Imports Gtk
Imports System.Diagnostics
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    ''' <summary>
    ''' Displays SimpleIDE's built-in Help content (resource links, keyboard shortcuts,
    ''' etc.) using native GTK widgets, and opens external documentation URLs in the
    ''' system's default web browser
    ''' </summary>
    Public Class HelpBrowser
        Inherits Box

        ''' <summary>
        ''' Identifies the Home page's URL, used for dedup checks and history tracking
        ''' </summary>
        Public Const HOME_URL As String = "simpleide://home"

        ''' <summary>
        ''' Identifies what kind of content a PageEntry holds
        ''' </summary>
        Private Enum PageKind
            eUnspecified
            eSections
            eHtmlUrl
            eLastValue
        End Enum

        ''' <summary>
        ''' Describes one navigable page in this browser's back/forward history
        ''' </summary>
        Private Class PageEntry
            Public Property Title As String
            Public Property Url As String
            Public Property Sections As List(Of HelpSection)
            Public Property Kind As PageKind = PageKind.eSections

            ' Populated after the first successful embedded (eHtmlUrl) load so Back/Forward
            ' can redisplay the exact same content instantly via LoadCachedPage, with no
            ' network fetch and so no way for revisiting a page to fail - Html Is Nothing
            ' means "not fetched yet" (or Reload() deliberately cleared it to force a
            ' fresh fetch)
            Public Property Html As String
            Public Property Resources As Dictionary(Of String, Byte())
        End Class

        ' Toolbar controls
        Private pUrlBar As CustomDrawTextBox
        Private pBackButton As CustomDrawButton
        Private pForwardButton As CustomDrawButton
        Private pRefreshButton As CustomDrawButton
        Private pHomeButton As CustomDrawButton
        Private pStatusLabel As Label

        ' Content area
        Private pContentBox As Box
        Private pScrolled As ScrolledWindow

        ' Embedded real-page renderer (litehtml), created lazily only if available -
        ' see NavigateToUrl/NavigateToUrlEmbedded
        Private pHtmlView As CustomDrawHtmlView

        ' History
        Private pHistory As New List(Of PageEntry)
        Private pHistoryIndex As Integer = -1

        Private pSettingsManager As SettingsManager
        Private pThemeManager As ThemeManager

        ' Events
        Public Event NavigationCompleted(vUrl As String)
        Public Event LoadingStateChanged(vIsLoading As Boolean)

        ''' <summary>
        ''' Gets the URL of the page currently displayed
        ''' </summary>
        Public ReadOnly Property CurrentUrl As String
            Get
                If pHistoryIndex >= 0 AndAlso pHistoryIndex < pHistory.Count Then
                    Return pHistory(pHistoryIndex).Url
                End If
                Return ""
            End Get
        End Property

        ''' <summary>
        ''' Gets the title of the page currently displayed
        ''' </summary>
        Public ReadOnly Property Title As String
            Get
                If pHistoryIndex >= 0 AndAlso pHistoryIndex < pHistory.Count Then
                    Return pHistory(pHistoryIndex).Title
                End If
                Return ""
            End Get
        End Property

        ''' <summary>
        ''' Gets whether content is currently loading - only meaningful for embedded
        ''' litehtml pages, since native section rendering is synchronous
        ''' </summary>
        Public ReadOnly Property IsLoading As Boolean
            Get
                Return pIsLoading
            End Get
        End Property

        Private pIsLoading As Boolean = False

        ''' <summary>
        ''' Gets whether there is an earlier page to navigate back to
        ''' </summary>
        Public ReadOnly Property CanGoBack As Boolean
            Get
                Return pHistoryIndex > 0
            End Get
        End Property

        ''' <summary>
        ''' Gets whether there is a later page to navigate forward to
        ''' </summary>
        Public ReadOnly Property CanGoForward As Boolean
            Get
                Return pHistoryIndex >= 0 AndAlso pHistoryIndex < pHistory.Count - 1
            End Get
        End Property

        ''' <summary>
        ''' Initializes a new HelpBrowser
        ''' </summary>
        ''' <param name="vSettingsManager">The shared settings manager</param>
        Public Sub New(vSettingsManager As SettingsManager)
            MyBase.New(Orientation.Vertical, 0)
            pSettingsManager = vSettingsManager
            Try
                BuildUI()
                ConnectEvents()
                ' Deliberately no initial navigation here - both call sites (OpenHelpTab,
                ' OpenHelpTabWithSections) call SetThemeManager immediately after
                ' construction and then explicitly navigate themselves.
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser: error initializing: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds the toolbar and native content area
        ''' </summary>
        Private Sub BuildUI()
            Try
                Orientation = Orientation.Vertical
                Spacing = 0

                ' Toolbar - a plain Box, not a native Gtk.Toolbar, so its buttons can be
                ' beveled CustomDrawButtons like every other panel's toolbar in this app
                ' (was ToolButton - flat, system icon-theme contrast)
                Dim lToolbar As New Box(Orientation.Horizontal, 2)
                lToolbar.MarginStart = 4
                lToolbar.MarginEnd = 4
                lToolbar.MarginTop = 2
                lToolbar.MarginBottom = 2

                pBackButton = New CustomDrawButton("", LoadToolIconPixbuf("go-previous"))
                pBackButton.TooltipText = "Go back"
                pBackButton.Sensitive = False

                pForwardButton = New CustomDrawButton("", LoadToolIconPixbuf("go-next"))
                pForwardButton.TooltipText = "Go forward"
                pForwardButton.Sensitive = False

                pRefreshButton = New CustomDrawButton("", LoadToolIconPixbuf("view-refresh"))
                pRefreshButton.TooltipText = "Refresh page"

                pHomeButton = New CustomDrawButton("", LoadToolIconPixbuf("go-home"))
                pHomeButton.TooltipText = "Go to home page"

                lToolbar.PackStart(pBackButton, False, False, 0)
                lToolbar.PackStart(pForwardButton, False, False, 0)
                lToolbar.PackStart(pRefreshButton, False, False, 0)
                lToolbar.PackStart(pHomeButton, False, False, 0)

                lToolbar.PackStart(New Separator(Orientation.Vertical), False, False, 4)

                ' URL bar - opens whatever's entered in the system's default browser
                pUrlBar = New CustomDrawTextBox("Enter a URL to open in your browser...")
                pUrlBar.WidthRequest = 300
                lToolbar.PackStart(pUrlBar, True, True, 0)

                PackStart(lToolbar, False, False, 0)

                ' Native content area
                pContentBox = New Box(Orientation.Vertical, 0)
                pContentBox.BorderWidth = 16

                pScrolled = New ScrolledWindow()
                pScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                pScrolled.Add(pContentBox)
                ' Same reasoning as pHtmlView's NoShowAll (see EnsureHtmlViewCreated): without
                ' this, any ancestor's ShowAll() call - e.g. OpenHelpTab's own
                ' pNotebook.ShowAll() right after creating this tab - force-shows pScrolled
                ' again even while an embedded page is what's supposed to be showing instead,
                ' since ShowAll() propagation isn't aware of ShowNativeContent/ShowHtmlContent's
                ' own explicit Hide()/Show() toggling. Confirmed live: without this, pScrolled
                ' and pHtmlView could both end up Visible at once, showing Home's content
                ' stacked above the embedded page's
                pScrolled.NoShowAll = True

                PackStart(pScrolled, True, True, 0)

                ' Status bar
                Dim lStatusBox As New Box(Orientation.Horizontal, 5)
                lStatusBox.BorderWidth = 2

                pStatusLabel = New Label("Ready")
                pStatusLabel.Halign = Align.Start
                lStatusBox.PackStart(pStatusLabel, True, True, 0)

                PackEnd(lStatusBox, False, False, 0)

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.BuildUI error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Wires up toolbar button and entry events
        ''' </summary>
        Private Sub ConnectEvents()
            Try
                AddHandler pBackButton.Clicked, AddressOf OnBackClicked
                AddHandler pForwardButton.Clicked, AddressOf OnForwardClicked
                AddHandler pRefreshButton.Clicked, AddressOf OnRefreshClicked
                AddHandler pHomeButton.Clicked, AddressOf OnHomeClicked
                AddHandler pUrlBar.Activated, AddressOf OnUrlActivated
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.ConnectEvents error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnBackClicked(vSender As Object, vArgs As EventArgs)
            GoBack()
        End Sub

        Private Sub OnForwardClicked(vSender As Object, vArgs As EventArgs)
            GoForward()
        End Sub

        Private Sub OnRefreshClicked(vSender As Object, vArgs As EventArgs)
            Reload()
        End Sub

        Private Sub OnHomeClicked(vSender As Object, vArgs As EventArgs)
            NavigateToHome()
        End Sub

        Private Sub OnUrlActivated(vSender As Object, vArgs As EventArgs)
            Try
                Dim lUrl As String = pUrlBar.Text.Trim()
                If Not String.IsNullOrEmpty(lUrl) Then
                    If Not lUrl.StartsWith("http://") AndAlso Not lUrl.StartsWith("https://") Then
                        lUrl = "https://" & lUrl
                    End If
                    NavigateToUrl(lUrl)
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnUrlActivated error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Wires the shared ThemeManager into this browser's CustomDraw controls; the
        ''' native content area (Labels, Buttons, Frames) already follows the app-wide
        ''' theme automatically via ThemeManager's global screen-level CSS provider
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                If pUrlBar IsNot Nothing Then pUrlBar.ThemeManager = vThemeManager
                If pBackButton IsNot Nothing Then pBackButton.ThemeManager = vThemeManager
                If pForwardButton IsNot Nothing Then pForwardButton.ThemeManager = vThemeManager
                If pRefreshButton IsNot Nothing Then pRefreshButton.ThemeManager = vThemeManager
                If pHomeButton IsNot Nothing Then pHomeButton.ThemeManager = vThemeManager
                If pHtmlView IsNot Nothing Then pHtmlView.SetThemeManager(vThemeManager)
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Loads a 16px icon-theme icon for a toolbar button - CustomDrawButton's own
        ''' IconContrastHelper auto-inverts it for dark/light contrast
        ''' </summary>
        ''' <param name="vIconName">Icon-theme name to look up</param>
        Private Function LoadToolIconPixbuf(vIconName As String) As Gdk.Pixbuf
            Try
                Return Gtk.IconTheme.Default.LoadIcon(vIconName, 16, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.LoadToolIconPixbuf error ({vIconName}): {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Navigates to the built-in Home page
        ''' </summary>
        Public Sub NavigateToHome()
            ShowSections("SimpleIDE Help", BuildHomeSections(), HOME_URL)
        End Sub

        ''' <summary>
        ''' Displays a set of built-in sections as a page, pushing it onto the back/forward
        ''' history (or refreshing in place if it's the same page already showing)
        ''' </summary>
        ''' <param name="vTitle">The page's title</param>
        ''' <param name="vSections">The sections to render</param>
        ''' <param name="vUrl">Optional identifying URL; defaults to a synthetic simpleide:// URL derived from the title</param>
        Public Sub ShowSections(vTitle As String, vSections As List(Of HelpSection), Optional vUrl As String = "")
            Try
                Dim lUrl As String = If(String.IsNullOrEmpty(vUrl), $"simpleide://{vTitle}", vUrl)
                Dim lEntry As New PageEntry With {.Title = vTitle, .Url = lUrl, .Sections = vSections}

                If pHistoryIndex >= 0 AndAlso pHistoryIndex < pHistory.Count AndAlso pHistory(pHistoryIndex).Url = lUrl Then
                    ' Re-navigating to the page already showing - refresh its content in place
                    pHistory(pHistoryIndex) = lEntry
                Else
                    ' Truncate any forward history before branching to a new page
                    If pHistoryIndex < pHistory.Count - 1 Then
                        pHistory.RemoveRange(pHistoryIndex + 1, pHistory.Count - pHistoryIndex - 1)
                    End If
                    pHistory.Add(lEntry)
                    pHistoryIndex = pHistory.Count - 1
                End If

                RenderCurrentPage()

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.ShowSections error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Opens vUrl - embedded via litehtml rendering if the native shim is available
        ''' on this system, otherwise in the user's default web browser with an in-app
        ''' confirmation page (the pre-litehtml behavior)
        ''' </summary>
        ''' <param name="vUrl">The URL to open</param>
        Public Sub NavigateToUrl(vUrl As String)
            Try
                If String.IsNullOrEmpty(vUrl) Then Return

                If CustomDrawHtmlView.IsAvailable Then
                    NavigateToUrlEmbedded(vUrl)
                Else
                    NavigateToUrlExternal(vUrl)
                End If

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToUrl error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Pushes vUrl onto history as an embedded litehtml page and begins loading it -
        ''' RenderCurrentPage drives the actual fetch/render via pHtmlView
        ''' </summary>
        ''' <param name="vUrl">The URL to load</param>
        Private Sub NavigateToUrlEmbedded(vUrl As String)
            Try
                Dim lEntry As New PageEntry With {.Title = vUrl, .Url = vUrl, .Kind = PageKind.eHtmlUrl}

                If pHistoryIndex >= 0 AndAlso pHistoryIndex < pHistory.Count AndAlso pHistory(pHistoryIndex).Url = vUrl Then
                    pHistory(pHistoryIndex) = lEntry
                Else
                    If pHistoryIndex < pHistory.Count - 1 Then
                        pHistory.RemoveRange(pHistoryIndex + 1, pHistory.Count - pHistoryIndex - 1)
                    End If
                    pHistory.Add(lEntry)
                    pHistoryIndex = pHistory.Count - 1
                End If

                RenderCurrentPage()

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToUrlEmbedded error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Opens vUrl in the system's default web browser and shows a small in-app
        ''' confirmation page - used when litehtml rendering is unavailable, or as the
        ''' fallback when an embedded page fails to load
        ''' </summary>
        ''' <param name="vUrl">The URL to open</param>
        Private Sub NavigateToUrlExternal(vUrl As String)
            Try
                OpenExternalUrl(vUrl)

                Dim lSection As New HelpSection("Opened in Your Browser")
                lSection.Items.Add(New HelpResourceItem(vUrl, "This link was opened in your system's default web browser.", vUrl))
                ShowSections("Opened Externally", New List(Of HelpSection) From {lSection}, vUrl)

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToUrlExternal error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Maps a known help topic identifier to a documentation URL and opens it
        ''' </summary>
        ''' <param name="vTopic">The topic identifier</param>
        Public Sub NavigateToTopic(vTopic As String)
            Try
                Select Case vTopic.ToLower()
                    Case "getting-started"
                        NavigateToUrl("https://learn.microsoft.com/en-us/dotnet/visual-basic/getting-started/")
                    Case "language-reference"
                        NavigateToUrl("https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/")
                    Case "gtk-sharp"
                        NavigateToUrl("https://www.mono-project.com/docs/GUI/gtksharp/")
                    Case Else
                        NavigateToUrl($"https://learn.microsoft.com/en-us/search/?terms={Uri.EscapeDataString(vTopic)}&category=All")
                End Select
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToTopic error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Navigates to the previous page in history, if any
        ''' </summary>
        Public Sub GoBack()
            Try
                If CanGoBack Then
                    pHistoryIndex -= 1
                    RenderCurrentPage()
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.GoBack error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Navigates to the next page in history, if any
        ''' </summary>
        Public Sub GoForward()
            Try
                If CanGoForward Then
                    pHistoryIndex += 1
                    RenderCurrentPage()
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.GoForward error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Re-renders the current page - for an embedded page, clears its cached content
        ''' first so this always forces a fresh network fetch (Back/Forward deliberately do
        ''' not; see RenderHtmlPageAsync)
        ''' </summary>
        Public Sub Reload()
            Try
                If pHistoryIndex >= 0 AndAlso pHistoryIndex < pHistory.Count Then
                    pHistory(pHistoryIndex).Html = Nothing
                    pHistory(pHistoryIndex).Resources = Nothing
                End If
                RenderCurrentPage()
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.Reload error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Opens vUrl in the user's system default web browser
        ''' </summary>
        ''' <param name="vUrl">The URL to open</param>
        Private Sub OpenExternalUrl(vUrl As String)
            Try
                Process.Start(New ProcessStartInfo With {
                    .FileName = vUrl,
                    .UseShellExecute = True
                })
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OpenExternalUrl: failed to open {vUrl}: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Renders the page at pHistoryIndex (native sections or an embedded litehtml
        ''' page, per its Kind) and updates toolbar state
        ''' </summary>
        Private Async Sub RenderCurrentPage()
            Try
                pIsLoading = True
                RaiseEvent LoadingStateChanged(True)

                If pHistoryIndex < 0 OrElse pHistoryIndex >= pHistory.Count Then
                    pIsLoading = False
                    RaiseEvent LoadingStateChanged(False)
                    Return
                End If

                Dim lPage As PageEntry = pHistory(pHistoryIndex)
                pUrlBar.Text = lPage.Url
                pBackButton.Sensitive = CanGoBack
                pForwardButton.Sensitive = CanGoForward

                If lPage.Kind = PageKind.eHtmlUrl Then
                    Await RenderHtmlPageAsync(lPage)
                Else
                    RenderSectionsPage(lPage)
                End If

                pIsLoading = False
                RaiseEvent LoadingStateChanged(False)
                RaiseEvent NavigationCompleted(lPage.Url)

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.RenderCurrentPage error: {ex.Message}")
                pIsLoading = False
                RaiseEvent LoadingStateChanged(False)
            End Try
        End Sub

        ''' <summary>
        ''' Rebuilds pContentBox from vPage's sections and shows the native content area
        ''' </summary>
        ''' <param name="vPage">The sections page to render</param>
        Private Sub RenderSectionsPage(vPage As PageEntry)
            Try
                ShowNativeContent()

                For Each lChild As Widget In pContentBox.Children
                    pContentBox.Remove(lChild)
                    lChild.Destroy()
                Next

                Dim lTitleLabel As New Label()
                lTitleLabel.Markup = $"<span size='xx-large' weight='bold'>{GLib.Markup.EscapeText(vPage.Title)}</span>"
                lTitleLabel.Xalign = 0
                lTitleLabel.MarginBottom = 12
                pContentBox.PackStart(lTitleLabel, False, False, 0)

                For Each lSection As HelpSection In vPage.Sections
                    pContentBox.PackStart(BuildSectionWidget(lSection), False, False, 0)
                Next

                pContentBox.ShowAll()
                pStatusLabel.Text = vPage.Title

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.RenderSectionsPage error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Shows the embedded litehtml view and displays vPage - if it was already fetched
        ''' once (Html is cached), redisplays that exact content via LoadCachedPage with no
        ''' network involved at all, so Back/Forward can never fail or re-open the external
        ''' browser just because a page that loaded fine the first time hit a transient
        ''' network problem on a later revisit. Only a genuinely new page (Html Is Nothing -
        ''' a fresh NavigateToUrlEmbedded push, or after Reload() clears the cache) actually
        ''' fetches, and on failure falls back to NavigateToUrlExternal (which replaces this
        ''' same history entry in place, since the URL is unchanged)
        ''' </summary>
        ''' <param name="vPage">The HTML page to render</param>
        Private Async Function RenderHtmlPageAsync(vPage As PageEntry) As Task
            Try
                EnsureHtmlViewCreated()
                ShowHtmlContent()

                If vPage.Html IsNot Nothing Then
                    pHtmlView.LoadCachedPage(vPage.Html, vPage.Url, vPage.Resources)
                    Return
                End If

                pStatusLabel.Text = $"Loading {vPage.Url}..."
                Await pHtmlView.NavigateAsync(vPage.Url)

                ' Cache the fetched content on this history entry (only on success -
                ' NavigateAsync only updates LastFetchResult when the fetch succeeded) so a
                ' later Back/Forward to this same entry redisplays instantly, above, instead
                ' of re-fetching
                Dim lFetched As HtmlPageFetchResult = pHtmlView.LastFetchResult
                If lFetched IsNot Nothing AndAlso lFetched.Success AndAlso lFetched.BaseUrl = pHtmlView.CurrentUrl Then
                    vPage.Html = lFetched.Html
                    vPage.Resources = lFetched.Resources
                End If

            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.RenderHtmlPageAsync error: {ex.Message}")
            End Try
        End Function

        ''' <summary>
        ''' Creates pHtmlView and wires its events on first use - only ever called after
        ''' confirming CustomDrawHtmlView.IsAvailable
        ''' </summary>
        Private Sub EnsureHtmlViewCreated()
            If pHtmlView IsNot Nothing Then Return

            pHtmlView = New CustomDrawHtmlView()
            pHtmlView.NoShowAll = True
            If pThemeManager IsNot Nothing Then pHtmlView.SetThemeManager(pThemeManager)

            AddHandler pHtmlView.LinkClicked, AddressOf OnHtmlViewLinkClicked
            AddHandler pHtmlView.LoadCompleted, AddressOf OnHtmlViewLoadCompleted
            AddHandler pHtmlView.LoadFailed, AddressOf OnHtmlViewLoadFailed

            PackStart(pHtmlView, True, True, 0)
        End Sub

        ''' <summary>
        ''' Shows the native sections ScrolledWindow and hides the embedded HTML view
        ''' </summary>
        Private Sub ShowNativeContent()
            If pHtmlView IsNot Nothing Then pHtmlView.Hide()
            pScrolled.Show()
        End Sub

        ''' <summary>
        ''' Shows the embedded HTML view and hides the native sections ScrolledWindow
        ''' </summary>
        Private Sub ShowHtmlContent()
            pScrolled.Hide()
            ' Deliberately Show(), not ShowAll() - pHtmlView.NoShowAll is set (so an
            ' ancestor's ShowAll doesn't force it visible while Home is showing), and GTK's
            ' ShowAll() also no-ops when called directly on a widget that itself has
            ' NoShowAll set, not just when propagating from an ancestor. ShowAll() worked
            ' the first time only because the widget was already Visible from its own
            ' constructor and had never been Hidden yet - after the first trip through
            ' ShowNativeContent() actually Hides it, ShowAll() here would never bring it
            ' back. Show() is never blocked by NoShowAll, and pHtmlView's own children were
            ' already made visible once by its constructor's own ShowAll() call, so a plain
            ' Show() on the outer widget is all that's needed.
            pHtmlView.Show()
        End Sub

        ''' <summary>
        ''' Follows a link clicked inside an embedded litehtml page the same way any other
        ''' navigation is handled
        ''' </summary>
        ''' <param name="vUrl">The clicked link's absolute URL</param>
        Private Sub OnHtmlViewLinkClicked(vUrl As String)
            NavigateToUrl(vUrl)
        End Sub

        ''' <summary>
        ''' Updates the status bar once an embedded page finishes loading
        ''' </summary>
        ''' <param name="vUrl">The URL that finished loading</param>
        Private Sub OnHtmlViewLoadCompleted(vUrl As String)
            Try
                pStatusLabel.Text = vUrl
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnHtmlViewLoadCompleted error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gracefully falls back to opening vUrl in the system browser when embedded
        ''' fetch/render fails - replaces the failed history entry in place
        ''' </summary>
        ''' <param name="vUrl">The URL that failed to load</param>
        ''' <param name="vError">The failure reason, logged for diagnostics</param>
        Private Sub OnHtmlViewLoadFailed(vUrl As String, vError As String)
            Try
                Console.WriteLine($"HelpBrowser: embedded load failed for {vUrl}: {vError}")
                NavigateToUrlExternal(vUrl)
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnHtmlViewLoadFailed error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds the widget for one section: a framed header plus its item rows
        ''' </summary>
        ''' <param name="vSection">The section to render</param>
        Private Function BuildSectionWidget(vSection As HelpSection) As Widget
            Dim lFrame As New Frame()
            lFrame.ShadowType = ShadowType.EtchedIn
            lFrame.MarginBottom = 16

            Dim lBox As New Box(Orientation.Vertical, 8)
            lBox.BorderWidth = 12

            Dim lHeaderLabel As New Label()
            lHeaderLabel.Markup = $"<span size='large' weight='bold'>{GLib.Markup.EscapeText(vSection.HeaderText)}</span>"
            lHeaderLabel.Xalign = 0
            lHeaderLabel.MarginBottom = 6
            lBox.PackStart(lHeaderLabel, False, False, 0)

            ' Lay items out two-per-row so sections read as two columns instead of one
            ' long list
            Dim lGrid As New Grid()
            lGrid.ColumnSpacing = 24
            lGrid.RowSpacing = 4
            lGrid.ColumnHomogeneous = True

            For lIndex As Integer = 0 To vSection.Items.Count - 1
                Dim lItemWidget As Widget = BuildItemWidget(vSection.Items(lIndex))
                lItemWidget.Hexpand = True
                lGrid.Attach(lItemWidget, lIndex Mod 2, lIndex \ 2, 1, 1)
            Next

            lBox.PackStart(lGrid, False, False, 0)

            lFrame.Add(lBox)
            Return lFrame
        End Function

        ''' <summary>
        ''' Builds the widget for one item row - a clickable link (Url set) or a plain
        ''' key/description row (Url empty, e.g. a keyboard shortcut)
        ''' </summary>
        ''' <param name="vItem">The item to render</param>
        Private Function BuildItemWidget(vItem As HelpResourceItem) As Widget
            Dim lItemBox As New Box(Orientation.Vertical, 2)
            lItemBox.MarginBottom = 8

            If Not String.IsNullOrEmpty(vItem.Url) Then
                Dim lLinkLabel As New Label()
                lLinkLabel.Markup = $"<span underline='single' foreground='#3498db'>{GLib.Markup.EscapeText(vItem.Title)}</span>"
                lLinkLabel.Xalign = 0
                lLinkLabel.Halign = Align.Start

                Dim lUrl As String = vItem.Url
                Dim lLink As Widget = MakeClickable(lLinkLabel, Sub() NavigateToUrl(lUrl))
                lLink.TooltipText = vItem.Url

                lItemBox.PackStart(lLink, False, False, 0)
            ElseIf Not String.IsNullOrEmpty(vItem.Title) Then
                Dim lTitleLabel As New Label()
                lTitleLabel.Markup = $"<tt><b>{GLib.Markup.EscapeText(vItem.Title)}</b></tt>"
                lTitleLabel.Xalign = 0
                lTitleLabel.Halign = Align.Start
                lItemBox.PackStart(lTitleLabel, False, False, 0)
            End If

            If Not String.IsNullOrEmpty(vItem.Description) Then
                Dim lDescLabel As New Label(vItem.Description)
                lDescLabel.Xalign = 0
                lDescLabel.Halign = Align.Start
                lDescLabel.LineWrap = True
                lItemBox.PackStart(lDescLabel, False, False, 0)
            End If

            Return lItemBox
        End Function

        ''' <summary>
        ''' Wraps vWidget in a transparent EventBox that runs vOnClick on click and shows
        ''' a hand cursor on hover - used instead of a real Button so resource links read
        ''' as plain hyperlinks rather than looking like clunky buttons
        ''' </summary>
        ''' <param name="vWidget">The widget (typically a Label) to make clickable</param>
        ''' <param name="vOnClick">The action to run when clicked</param>
        Private Function MakeClickable(vWidget As Widget, vOnClick As System.Action) As Widget
            Dim lEventBox As New EventBox()
            lEventBox.Add(vWidget)
            lEventBox.VisibleWindow = False
            lEventBox.Halign = Align.Start

            AddHandler lEventBox.ButtonPressEvent, Sub(vSender As Object, vArgs As ButtonPressEventArgs)
                vOnClick()
                vArgs.RetVal = True
            End Sub

            AddHandler lEventBox.EnterNotifyEvent, Sub(vSender As Object, vArgs As EnterNotifyEventArgs)
                Try
                    Dim lWindow As Gdk.Window = lEventBox.Window
                    If lWindow IsNot Nothing Then
                        Dim lDisplay As Gdk.Display = Gdk.Display.Default
                        lWindow.Cursor = New Gdk.Cursor(lDisplay, Gdk.CursorType.Hand2)
                    End If
                Catch ex As Exception
                    Console.WriteLine($"HelpBrowser.MakeClickable: cursor error: {ex.Message}")
                End Try
                vArgs.RetVal = False
            End Sub

            AddHandler lEventBox.LeaveNotifyEvent, Sub(vSender As Object, vArgs As LeaveNotifyEventArgs)
                Try
                    Dim lWindow As Gdk.Window = lEventBox.Window
                    If lWindow IsNot Nothing Then
                        lWindow.Cursor = Nothing
                    End If
                Catch ex As Exception
                    Console.WriteLine($"HelpBrowser.MakeClickable: cursor error: {ex.Message}")
                End Try
            End Sub

            Return lEventBox
        End Function

        ''' <summary>
        ''' Builds the Home page's categorized resource sections
        ''' </summary>
        Private Function BuildHomeSections() As List(Of HelpSection)
            Dim lSections As New List(Of HelpSection)

            Dim lSyntax As New HelpSection("Syntax & Program Structure")
            lSyntax.Items.Add(New HelpResourceItem("VB.NET Language Reference", "The complete Visual Basic language reference", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/"))
            lSyntax.Items.Add(New HelpResourceItem("Getting Started with VB.NET", "Core language basics for newcomers to VB.NET", "https://learn.microsoft.com/en-us/dotnet/visual-basic/getting-started/"))
            lSyntax.Items.Add(New HelpResourceItem("Statements", "Declaration, executable, and control-flow statement syntax", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/"))
            lSyntax.Items.Add(New HelpResourceItem("Declared Elements", "Rules for naming, scope, and declaring program elements", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/declared-elements/"))
            lSections.Add(lSyntax)

            Dim lKeywords As New HelpSection("Keyword & Data Type Definitions")
            lKeywords.Items.Add(New HelpResourceItem("Keywords (A-Z)", "Full alphabetical reference of every VB.NET keyword", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/keywords/"))
            lKeywords.Items.Add(New HelpResourceItem("Operators", "Arithmetic, comparison, logical, and bitwise operators", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/operators/"))
            lKeywords.Items.Add(New HelpResourceItem("Data Types", "Built-in types, ranges, and conversion rules", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/data-types/"))
            lKeywords.Items.Add(New HelpResourceItem("Modifiers", "Access, inheritance, and other declaration modifiers", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/modifiers/"))
            lSections.Add(lKeywords)

            Dim lPractices As New HelpSection("Programming Practices & Concepts")
            lPractices.Items.Add(New HelpResourceItem("VB.NET Programming Guide", "Concepts and patterns behind everyday VB.NET code", "https://learn.microsoft.com/en-us/dotnet/visual-basic/programming-guide/"))
            lPractices.Items.Add(New HelpResourceItem("Object-Oriented Programming", "Classes, inheritance, interfaces, and polymorphism", "https://learn.microsoft.com/en-us/dotnet/visual-basic/programming-guide/concepts/object-oriented-programming"))
            lPractices.Items.Add(New HelpResourceItem("Error & Exception Handling", "Structured exception handling with Try/Catch/Finally", "https://learn.microsoft.com/en-us/dotnet/visual-basic/language-reference/statements/try-catch-finally-statement"))
            lPractices.Items.Add(New HelpResourceItem("LINQ in Visual Basic", "Querying objects, XML, and data with LINQ syntax", "https://learn.microsoft.com/en-us/dotnet/visual-basic/programming-guide/language-features/linq/"))
            lSections.Add(lPractices)

            Dim lLibraries As New HelpSection("Function & Class Libraries")
            lLibraries.Items.Add(New HelpResourceItem(".NET 8 API Browser", "Browse every .NET class, namespace, and member", "https://learn.microsoft.com/en-us/dotnet/api/?view=net-8.0"))
            lLibraries.Items.Add(New HelpResourceItem("Microsoft.VisualBasic Namespace", "VB-native runtime functions like Format, MsgBox, and Strings", "https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualbasic?view=net-8.0"))
            lLibraries.Items.Add(New HelpResourceItem("System Namespace", "Core base types, math, and console I/O", "https://learn.microsoft.com/en-us/dotnet/api/system?view=net-8.0"))
            lLibraries.Items.Add(New HelpResourceItem(".NET Standard Library Tour", "A guided overview of the built-in class library", "https://learn.microsoft.com/en-us/dotnet/standard/tour"))
            lSections.Add(lLibraries)

            Dim lExamples As New HelpSection("Examples & Tutorials")
            lExamples.Items.Add(New HelpResourceItem("VB.NET Console App Tutorial", "Build and run your first VB.NET console application", "https://learn.microsoft.com/en-us/dotnet/visual-basic/getting-started/console-application"))
            lExamples.Items.Add(New HelpResourceItem(".NET Samples on GitHub", "Official runnable sample code for .NET, including VB.NET", "https://github.com/dotnet/samples"))
            lExamples.Items.Add(New HelpResourceItem("Stack Overflow - VB.NET", "Community questions and answers tagged vb.net", "https://stackoverflow.com/questions/tagged/vb.net"))
            lSections.Add(lExamples)

            Dim lGtk As New HelpSection("GTK# Development")
            lGtk.Items.Add(New HelpResourceItem("GTK# documentation", "Official GTK# documentation", "https://www.mono-project.com/docs/GUI/gtksharp/"))
            lGtk.Items.Add(New HelpResourceItem("GTK 3 Reference", "Complete GTK+ 3 API Reference", "https://docs.gtk.org/gtk3/"))
            lGtk.Items.Add(New HelpResourceItem("GTK Widget Gallery", "Visual index of all GTK widgets", "https://docs.gtk.org/gtk3/visual_index.html"))
            lGtk.Items.Add(New HelpResourceItem("DevDocs GTK", "Fast, offline-capable documentation browser", "https://devdocs.io/gtk~3.20/"))
            lSections.Add(lGtk)

            Dim lDotNetCli As New HelpSection(".NET Core & CLI")
            lDotNetCli.Items.Add(New HelpResourceItem(".NET documentation", "Main .NET documentation portal", "https://learn.microsoft.com/en-us/dotnet/"))
            lDotNetCli.Items.Add(New HelpResourceItem(".NET CLI Reference", "Command-line interface documentation", "https://learn.microsoft.com/en-us/dotnet/core/tools/"))
            lDotNetCli.Items.Add(New HelpResourceItem(".NET Diagnostics", "Debugging and diagnostic tools", "https://learn.microsoft.com/en-us/dotnet/core/diagnostics/"))
            lDotNetCli.Items.Add(New HelpResourceItem("NuGet Gallery", "Browse and search .NET packages", "https://www.nuget.org/"))
            lSections.Add(lDotNetCli)

            Dim lAdditional As New HelpSection("Additional Resources")
            lAdditional.Items.Add(New HelpResourceItem("Stack Overflow - GTK#", "Community Q&A for GTK# development", "https://stackoverflow.com/questions/tagged/gtk%23"))
            lAdditional.Items.Add(New HelpResourceItem("GTK# GitHub Repository", "Source code and issue tracker", "https://github.com/GtkSharp/GtkSharp"))
            lAdditional.Items.Add(New HelpResourceItem("GTK# Widget Examples", "Code examples for common widgets", "https://www.mono-project.com/docs/GUI/gtksharp/widgets/buttons/"))
            lAdditional.Items.Add(New HelpResourceItem(".NET Porting Guide", "Migrating from .NET Framework to .NET Core", "https://learn.microsoft.com/en-us/dotnet/core/porting/"))
            lSections.Add(lAdditional)

            Dim lLinux As New HelpSection("Linux Development")
            lLinux.Items.Add(New HelpResourceItem(".NET on Linux", "Installing and using .NET on Linux", "https://learn.microsoft.com/en-us/dotnet/core/install/linux"))
            lLinux.Items.Add(New HelpResourceItem("Mono documentation", "Cross-platform .NET framework", "https://www.mono-project.com/docs/"))
            lLinux.Items.Add(New HelpResourceItem("Linux Deployment", "Deploying .NET apps on Linux", "https://learn.microsoft.com/en-us/dotnet/core/deploying/linux"))
            lLinux.Items.Add(New HelpResourceItem("VS Code .NET Support", "Alternative editor for .NET development", "https://code.visualstudio.com/docs/languages/dotnet"))
            lSections.Add(lLinux)

            Return lSections
        End Function

    End Class

End Namespace
