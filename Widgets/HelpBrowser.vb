' HelpBrowser.vb - WebKit-based help browser for SimpleIDE
Imports Gtk
Imports WebKit
Imports System.Diagnostics

Namespace Widgets
    
    Public Class HelpBrowser
        Inherits Box
        
        ' Private fields
        Private pWebView As WebView
        Private pUrlBar As Entry
        Private pSearchEntry As Entry
        Private pBackButton As Button
        Private pForwardButton As Button
        Private pRefreshButton As Button
        Private pHomeButton As Button
        Private pExternalButton As Button
        Private pProgressBar As ProgressBar
        Private pStatusLabel As Label
        
        ' Constants
        Private Const HOME_HTML As String = "<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <Title>SimpleIDE Help</Title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 20px;
            background: #f5f5f5;
            Color: #333;
        }
        .container {
            max-Width: 1200px;
            margin: 0 auto;
        }
        h1 {
            Color: #2c3e50;
            margin-bottom: 30px;
        }
        .section {
            background: white;
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        h2 {
            Color: #34495e;
            margin-top: 0;
            border-bottom: 2px solid #ecf0f1;
            padding-bottom: 10px;
        }
        .links {
            display: grid;
            grid-Template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 15px;
            margin-top: 15px;
        }
        a {
            display: block;
            padding: 12px;
            background: #3498db;
            Color: white;
            Text-decoration: none;
            border-radius: 5px;
            transition: background 0.3s;
        }
        a:hover {
            background: #2980b9;
        }
        .Description {
            font-size: 0.9em;
            opacity: 0.9;
            margin-top: 3px;
        }
        .search-tip {
            background: #e8f4f8;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 20px;
        }
    </style>
</head>
<body>
    <div Class='container'>
        <h1>SimpleIDE Help Resources</h1>
        
        <div Class='search-tip'>
            <strong>Tip:</strong> Use the search box above To search documentation. For example, Try searching For 'TreeView', 'TextBuffer', or 'async await'.
        </div>
        
        <div Class='section'>
            <h2>Visual Basic .NET</h2>
            <div Class='links'>
                <a href='https://learn.microsoft.com/en-us/dotnet/Visual-basic/'>
                    VB.NET Language Reference
                    <div Class='Description'>Complete Visual Basic .NET documentation</div>
                </a>
                <a href='https://learn.microsoft.com/en-us/dotnet/Visual-basic/programming-guide/'>
                    VB.NET Programming Guide
                    <div Class='Description'>Detailed programming concepts and examples</div>
                </a>
                <a href='https://learn.microsoft.com/en-us/dotnet/Visual-basic/Language-Reference/'>
                    Language Reference
                    <div Class='Description'>Keywords, Operators, and statements Reference</div>
                </a>
                <a href='https://learn.microsoft.com/en-us/dotnet/api/?view=net-8.0'>
                    .NET 8 API Browser
                    <div Class='Description'>Browse .NET classes and namespaces</div>
                </a>
            </div>
        </div>
        
        <div Class='section'>
            <h2>GTK# Development</h2>
            <div Class='links'>
                <a href='https://www.mono-project.com/docs/GUI/gtksharp/'>
                    GTK# documentation
                    <div Class='Description'>Official GTK# documentation</div>
                </a>
                <a href='https://docs.gtk.org/gtk3/'>
                    GTK 3 Reference
                    <div Class='Description'>Complete GTK+ 3 API Reference</div>
                </a>
                <a href='https://docs.gtk.org/gtk3/visual_index.html'>
                    GTK Widget Gallery
                    <div Class='Description'>Visual index of all GTK widgets</div>
                </a>
                <a href='https://devdocs.io/gtk~3.20/'>
                    DevDocs GTK
                    <div Class='Description'>Fast, offline-capable documentation browser</div>
                </a>
            </div>
        </div>
        
        <div Class='section'>
            <h2>.NET Core &amp; CLI</h2>
            <div Class='links'>
                <a href='https://learn.microsoft.com/en-us/dotnet/'>
                    .NET documentation
                    <div Class='Description'>Main .NET documentation portal</div>
                </a>
                <a href='https://learn.microsoft.com/en-us/dotnet/core/tools/'>
                    .NET CLI Reference
                    <div Class='Description'>Command-Line interface documentation</div>
                </a>
                <a href='https://learn.microsoft.com/en-us/dotnet/core/diagnostics/'>
                    .NET Diagnostics
                    <div Class='Description'>Debugging and diagnostic tools</div>
                </a>
                <a href='https://www.nuget.org/'>
                    NuGet Gallery
                    <div Class='Description'>Browse and search .NET Packages</div>
                </a>
            </div>
        </div>
        
        <div Class='section'>
            <h2>Additional Resources</h2>
            <div Class='links'>
                <a href='https://stackoverflow.com/questions/tagged/gtk%23'>
                    Stack Overflow - GTK#
                    <div Class='Description'>Community Q&amp;A for GTK# development</div>
                </a>
                <a href='https://github.com/GtkSharp/GtkSharp'>
                    GTK# GitHub Repository
                    <div Class='Description'>Source code and issue tracker</div>
                </a>
                <a href='https://www.mono-project.com/docs/GUI/gtksharp/widgets/buttons/'>
                    GTK# Widget Examples
                    <div Class='Description'>code examples for common widgets</div>
                </a>
                <a href='https://learn.microsoft.com/en-us/dotnet/core/porting/'>
                    .NET Porting Guide
                    <div Class='Description'>Migrating from .NET Framework to .NET Core</div>
                </a>
            </div>
        </div>
        
        <div Class='section'>
            <h2>SimpleIDE Features</h2>
            <div Class='links'>
                <a href='https://www.mono-project.com/docs/GUI/gtksharp/hello-world/'>
                    Getting Started With GTK#
                    <div Class='Description'>Create your first GTK# application</div>
                </a>
                <a href='https://docs.gtk.org/gtk3/class.TextView.html'>
                    GtkTextView documentation
                    <div Class='Description'>Text editing Widget used in SimpleIDE</div>
                </a>
                <a href='https://docs.gtk.org/gtk3/class.TreeView.html'>
                    GtkTreeView documentation
                    <div Class='Description'>Tree/list Widget for project explorer</div>
                </a>
                <a href='https://github.com/GtkSharp/GtkSharp/tree/develop/Source/Samples'>
                    GTK# code Samples
                    <div Class='Description'>Example code for common tasks</div>
                </a>
                <a href='https://developer.gnome.org/gtk3/stable/'>
                    GNOME Developer Docs
                    <div Class='Description'>Additional GTK documentation</div>
                </a>
            </div>
        </div>
        
        <div Class='section'>
            <h2>Linux Development</h2>
            <div Class='links'>
                <a href='https://learn.microsoft.com/en-us/dotnet/core/install/linux'>
                    .NET On Linux
                    <div Class='Description'>Installing and using .NET on Linux</div>
                </a>
                <a href='https://www.mono-project.com/docs/'>
                    Mono documentation
                    <div Class='Description'>Cross-Platform .NET framework</div>
                </a>
                <a href='https://learn.microsoft.com/en-us/dotnet/core/deploying/linux'>
                    Linux Deployment
                    <div Class='Description'>Deploying .NET apps on Linux</div>
                </a>
                <a href='https://code.visualstudio.com/docs/languages/dotnet'>
                    VS code .NET Support
                    <div Class='Description'>Alternative Editor for .NET development</div>
                </a>
            </div>
        </div>
    </div>
</body>
</html>"
        
        ' Events
        Public Event NavigationCompleted(vUrl As String)
        Public Event LoadingStateChanged(vIsLoading As Boolean)
        
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            
            Try
                BuildUI()
                ConnectEvents()
                
                ' Load home page
                NavigateToHome()
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser: error initializing: {ex.Message}")
            End Try
        End Sub
        
        Private Sub BuildUI()
            Try
                ' Create main toolbar
                Dim lToolbar As New Toolbar()
                lToolbar.ToolbarStyle = ToolbarStyle.Icons
                lToolbar.IconSize = IconSize.Menu
                
                ' Navigation buttons
                pBackButton = New Button()
                pBackButton.Image = Image.NewFromIconName("go-previous", IconSize.Menu)
                pBackButton.TooltipText = "Go back"
                pBackButton.Relief = ReliefStyle.None
                
                pForwardButton = New Button()
                pForwardButton.Image = Image.NewFromIconName("go-next", IconSize.Menu)
                pForwardButton.TooltipText = "Go forward"
                pForwardButton.Relief = ReliefStyle.None
                
                pRefreshButton = New Button()
                pRefreshButton.Image = Image.NewFromIconName("view-Refresh", IconSize.Menu)
                pRefreshButton.TooltipText = "Refresh"
                pRefreshButton.Relief = ReliefStyle.None
                
                pHomeButton = New Button()
                pHomeButton.Image = Image.NewFromIconName("go-home", IconSize.Menu)
                pHomeButton.TooltipText = "Home"
                pHomeButton.Relief = ReliefStyle.None
                
                pExternalButton = New Button()
                pExternalButton.Image = Image.NewFromIconName("applications-internet", IconSize.Menu)
                pExternalButton.TooltipText = "Open in external browser"
                pExternalButton.Relief = ReliefStyle.None
                
                ' URL bar
                pUrlBar = New Entry()
                pUrlBar.WidthRequest = 400
                pUrlBar.PlaceholderText = "Enter Url or topic..."
                
                ' Search entry
                Dim lSearchLabel As New Label("Search:")
                lSearchLabel.MarginStart = 10
                
                pSearchEntry = New Entry()
                pSearchEntry.WidthRequest = 200
                pSearchEntry.PlaceholderText = "Search documentation..."
                
                ' Add items to toolbar
                Dim lBackItem As New ToolItem()
                lBackItem.Add(pBackButton)
                lToolbar.Add(lBackItem)
                
                Dim lForwardItem As New ToolItem()
                lForwardItem.Add(pForwardButton)
                lToolbar.Add(lForwardItem)
                
                Dim lRefreshItem As New ToolItem()
                lRefreshItem.Add(pRefreshButton)
                lToolbar.Add(lRefreshItem)
                
                Dim lHomeItem As New ToolItem()
                lHomeItem.Add(pHomeButton)
                lToolbar.Add(lHomeItem)
                
                Dim lExternalItem As New ToolItem()
                lExternalItem.Add(pExternalButton)
                lToolbar.Add(lExternalItem)
                
                lToolbar.Add(New SeparatorToolItem())
                
                ' URL bar tool item
                Dim lUrlItem As New ToolItem()
                lUrlItem.Add(pUrlBar)
                lUrlItem.Expand = True
                lToolbar.Add(lUrlItem)
                
                lToolbar.Add(New SeparatorToolItem())
                
                ' Search items
                Dim lSearchLabelItem As New ToolItem()
                lSearchLabelItem.Add(lSearchLabel)
                lToolbar.Add(lSearchLabelItem)
                
                Dim lSearchItem As New ToolItem()
                lSearchItem.Add(pSearchEntry)
                lToolbar.Add(lSearchItem)
                
                ' Pack toolbar
                PackStart(lToolbar, False, False, 0)
                
                ' Create WebView
                pWebView = New WebView()
                
                ' Create scrolled window for WebView
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lScrolled.Add(pWebView)
                
                ' Pack scrolled window
                PackStart(lScrolled, True, True, 0)
                
                ' Status bar
                Dim lStatusBox As New Box(Orientation.Horizontal, 5)
                lStatusBox.BorderWidth = 2
                
                pProgressBar = New ProgressBar()
                pProgressBar.WidthRequest = 100
                pProgressBar.Visible = False
                lStatusBox.PackStart(pProgressBar, False, False, 0)
                
                pStatusLabel = New Label("Ready")
                pStatusLabel.Halign = Align.Start
                lStatusBox.PackStart(pStatusLabel, True, True, 0)
                
                PackEnd(lStatusBox, False, False, 0)
                
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.BuildUI: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ConnectEvents()
            Try
                ' Button events
                AddHandler pBackButton.Clicked, AddressOf OnBackClicked
                AddHandler pForwardButton.Clicked, AddressOf OnForwardClicked
                AddHandler pRefreshButton.Clicked, AddressOf OnRefreshClicked
                AddHandler pHomeButton.Clicked, AddressOf OnHomeClicked
                AddHandler pExternalButton.Clicked, AddressOf OnExternalClicked
                
                ' URL bar events
                AddHandler pUrlBar.Activated, AddressOf OnUrlActivated
                
                ' Search events
                AddHandler pSearchEntry.Activated, AddressOf OnSearchActivated
                
                ' WebView events
                AddHandler pWebView.LoadChanged, AddressOf OnLoadChanged
                AddHandler pWebView.LoadFailed, AddressOf OnLoadFailed
                AddHandler pWebView.DecidePolicy, AddressOf OnDecidePolicy
                
                ' Monitor property changes for progress and title
                ' FIXED: Use a timer to poll for changes instead of NotifySignal
                Dim lTimer As UInteger = GLib.Timeout.Add(100, Function()
                    Try
                        If pWebView.EstimatedLoadProgress > 0 AndAlso pWebView.EstimatedLoadProgress < 1.0 Then
                            OnLoadProgressChanged()
                        End If
                        
                        If Not String.IsNullOrEmpty(pWebView.Title) AndAlso pStatusLabel.Text <> pWebView.Title Then
                            OnTitleChanged()
                        End If
                    Catch ex As Exception
                        Console.WriteLine($"HelpBrowser Progress timer error: {ex.Message}")
                    End Try
                    Return True ' Continue timer
                End Function)
                
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.ConnectEvents: error: {ex.Message}")
            End Try
        End Sub
        
        ' Event handlers
        Private Sub OnBackClicked(sender As Object, e As EventArgs)
            Try
                If pWebView.CanGoBack Then
                    pWebView.GoBack()
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnBackClicked: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnForwardClicked(sender As Object, e As EventArgs)
            Try
                If pWebView.CanGoForward Then
                    pWebView.GoForward()
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnForwardClicked: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnRefreshClicked(sender As Object, e As EventArgs)
            Try
                pWebView.Reload()
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnRefreshClicked: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnHomeClicked(sender As Object, e As EventArgs)
            Try
                NavigateToHome()
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnHomeClicked: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnExternalClicked(sender As Object, e As EventArgs)
            Try
                Dim lCurrentUrl As String = pWebView.Uri
                If Not String.IsNullOrEmpty(lCurrentUrl) AndAlso (lCurrentUrl.StartsWith("http://") OrElse lCurrentUrl.StartsWith("https://")) Then
                    Process.Start(New ProcessStartInfo With {
                        .FileName = lCurrentUrl,
                        .UseShellExecute = True
                    })
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnExternalClicked: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnUrlActivated(sender As Object, e As EventArgs)
            Try
                Dim lUrl As String = pUrlBar.Text.Trim()
                If Not String.IsNullOrEmpty(lUrl) Then
                    ' Add protocol if missing
                    If Not lUrl.StartsWith("http://") AndAlso Not lUrl.StartsWith("https://") AndAlso Not lUrl.StartsWith("file://") Then
                        lUrl = "https://" & lUrl
                    End If
                    NavigateToUrl(lUrl)
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnUrlActivated: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSearchActivated(sender As Object, e As EventArgs)
            Try
                Dim lSearchTerm As String = pSearchEntry.Text.Trim()
                If Not String.IsNullOrEmpty(lSearchTerm) Then
                    ' Search on Microsoft Learn by default
                    Dim lSearchUrl As String = $"https://learn.microsoft.com/en-us/search/?terms={Uri.EscapeDataString(lSearchTerm)}&Category=documentation"
                    NavigateToUrl(lSearchUrl)
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnSearchActivated: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnLoadChanged(sender As Object, e As LoadChangedArgs)
            Try
                Select Case e.LoadEvent
                    Case LoadEvent.Started
                        pProgressBar.Visible = True
                        pStatusLabel.Text = "Loading..."
                        RaiseEvent LoadingStateChanged(True)
                    Case LoadEvent.Committed
                        pUrlBar.Text = pWebView.Uri
                    Case LoadEvent.Finished
                        pProgressBar.Visible = False
                        pStatusLabel.Text = "Ready"
                        UpdateNavigationButtons()
                        RaiseEvent LoadingStateChanged(False)
                        RaiseEvent NavigationCompleted(pWebView.Uri)
                End Select
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnLoadChanged: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnLoadFailed(sender As Object, e As LoadFailedArgs)
            Try
                pProgressBar.Visible = False
                pStatusLabel.Text = $"Failed to load: {e.FailingUri}"
                Console.WriteLine($"HelpBrowser.OnLoadFailed: Failed to load {e.FailingUri} - error: {e.error}")
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnLoadFailed: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnLoadProgressChanged()
            Try
                Dim lProgress As Double = pWebView.EstimatedLoadProgress
                pProgressBar.Fraction = lProgress
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnLoadProgressChanged: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnTitleChanged()
            Try
                Dim lTitle As String = pWebView.Title
                If Not String.IsNullOrEmpty(lTitle) Then
                    pStatusLabel.Text = lTitle
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnTitleChanged: error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDecidePolicy(sender As Object, e As DecidePolicyArgs)
            Try
                ' FIXED: Access the request properly from DecidePolicyArgs
                Dim lDecision As PolicyDecision = e.Decision
                Dim lDecisionType As PolicyDecisionType = e.DecisionType
                
                If lDecisionType = PolicyDecisionType.NavigationAction Then
                    ' Cast to NavigationPolicyDecision to access navigation details
                    Dim lNavDecision As NavigationPolicyDecision = CType(lDecision, NavigationPolicyDecision)
                    Dim lAction As NavigationAction = lNavDecision.NavigationAction
                    Dim lRequest As URIRequest = lAction.Request
                    Dim lUri As String = lRequest.Uri
                    
                    ' Allow local files, data URLs, and HTTP/HTTPS
                    If lUri.StartsWith("file://") OrElse lUri.StartsWith("http://") OrElse lUri.StartsWith("https://") OrElse lUri.StartsWith("Data:") Then
                        lNavDecision.Use() ' Allow navigation
                    Else
                        ' Open in external browser for other protocols
                        Process.Start(New ProcessStartInfo With {
                            .FileName = lUri,
                            .UseShellExecute = True
                        })
                        lNavDecision.Ignore() ' Prevent navigation in WebView
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.OnDecidePolicy: error: {ex.Message}")
                ' On error, allow navigation
                If e.Decision IsNot Nothing Then
                    e.Decision.Use()
                End If
            End Try
        End Sub
        
        Private Sub UpdateNavigationButtons()
            Try
                pBackButton.Sensitive = pWebView.CanGoBack
                pForwardButton.Sensitive = pWebView.CanGoForward
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.UpdateNavigationButtons: error: {ex.Message}")
            End Try
        End Sub
        
        ' Public methods
        Public Sub NavigateToUrl(vUrl As String)
            Try
                If Not String.IsNullOrEmpty(vUrl) Then
                    pWebView.LoadUri(vUrl)
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToUrl: error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub NavigateToHtml(vHtml As String, Optional vBaseUri As String = "")
            Try
                If Not String.IsNullOrEmpty(vHtml) Then
                    pWebView.LoadHtml(vHtml, vBaseUri)
                End If
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToHtml: error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub NavigateToHome()
            Try
                ' Load the home HTML directly
                pWebView.LoadHtml(HOME_HTML, "about:blank")
                pUrlBar.Text = "simpleide://home"
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToHome: error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub NavigateToTopic(vTopic As String)
            Try
                ' Map topic to URL
                Select Case vTopic.ToLower()
                    Case "getting-started"
                        NavigateToUrl("https://learn.microsoft.com/en-us/dotnet/Visual-basic/getting-started/")
                    Case "Language-Reference"
                        NavigateToUrl("https://learn.microsoft.com/en-us/dotnet/Visual-basic/Language-Reference/")
                    Case "gtk-sharp"
                        NavigateToUrl("https://www.mono-project.com/docs/GUI/gtksharp/")
                    Case Else
                        NavigateToUrl($"https://learn.microsoft.com/en-us/search/?terms={vTopic}&Category=All")
                End Select
            Catch ex As Exception
                Console.WriteLine($"HelpBrowser.NavigateToTopic: error: {ex.Message}")
            End Try
        End Sub
        
        ' Properties
        Public ReadOnly Property WebView As WebView
            Get
                Return pWebView
            End Get
        End Property
        
        Public ReadOnly Property CurrentUrl As String
            Get
                Return If(pWebView?.Uri, "")
            End Get
        End Property
        
        Public ReadOnly Property IsLoading As Boolean
            Get
                Return pWebView IsNot Nothing AndAlso pWebView.IsLoading
            End Get
        End Property
        
    End Class
    
End Namespace