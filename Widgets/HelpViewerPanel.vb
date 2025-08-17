' Widgets/HelpViewerPanel.vb - Simple help viewer without WebKit dependencies
Imports Gtk
Imports System
Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports SimpleIDE.Utilities

Namespace Widgets
    Public Class HelpViewerPanel
        Inherits Box
        
        ' Private fields
        Private pTextView As TextView
        Private pUrlCombo As ComboBoxText
        Private pBackButton As Button
        Private pForwardButton As Button
        Private pHomeButton As Button
        Private pStatusLabel As Label
        Private pProgressSpinner As Spinner
        Private pHttpClient As New HttpClient()
        Private pHistory As New List(Of String)
        Private pHistoryIndex As Integer = -1
        Private pQuickLinks As New Dictionary(Of String, String)
        
        ' Events
        Public Event TitleChanged(vTitle As String)
        
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            Initialize()
        End Sub
        
        Private Sub Initialize()
            ' Initialize quick links
            InitializeQuickLinks()
            
            ' Create toolbar
            Dim lToolbar As New Box(Orientation.Horizontal, 4)
            lToolbar.MarginStart = 6
            lToolbar.MarginEnd = 6
            lToolbar.MarginTop = 4
            lToolbar.MarginBottom = 4
            
            ' Navigation buttons
            pBackButton = New Button()
            pBackButton.Image = Image.NewFromIconName("go-previous", IconSize.SmallToolbar)
            pBackButton.TooltipText = "Go back"
            pBackButton.Sensitive = False
            AddHandler pBackButton.Clicked, AddressOf OnBackClicked
            lToolbar.PackStart(pBackButton, False, False, 0)
            
            pForwardButton = New Button()
            pForwardButton.Image = Image.NewFromIconName("go-next", IconSize.SmallToolbar)
            pForwardButton.TooltipText = "Go forward"
            pForwardButton.Sensitive = False
            AddHandler pForwardButton.Clicked, AddressOf OnForwardClicked
            lToolbar.PackStart(pForwardButton, False, False, 0)
            
            pHomeButton = New Button()
            pHomeButton.Image = Image.NewFromIconName("go-home", IconSize.SmallToolbar)
            pHomeButton.TooltipText = "Go to help home"
            AddHandler pHomeButton.Clicked, AddressOf OnHomeClicked
            lToolbar.PackStart(pHomeButton, False, False, 0)
            
            ' Separator
            Dim lSeparator As New Separator(Orientation.Vertical)
            lToolbar.PackStart(lSeparator, False, False, 6)
            
            ' Quick links dropdown
            pUrlCombo = New ComboBoxText()
            pUrlCombo.TooltipText = "Select a help topic or enter custom Url"
            
            ' Add quick links to dropdown
            For Each kvp In pQuickLinks
                pUrlCombo.AppendText(kvp.key)
            Next
            
            pUrlCombo.WidthRequest = 300
            AddHandler pUrlCombo.Changed, AddressOf OnUrlComboChanged
            lToolbar.PackStart(pUrlCombo, False, False, 0)
            
            ' Progress spinner
            pProgressSpinner = New Spinner()
            lToolbar.PackStart(pProgressSpinner, False, False, 4)
            
            ' Text view for content
            pTextView = New TextView()
            pTextView.Editable = False
            pTextView.WrapMode = WrapMode.Word
            pTextView.LeftMargin = 20
            pTextView.RightMargin = 20
            pTextView.TopMargin = 10
            pTextView.BottomMargin = 10
            
            ' Create text buffer with tags for formatting
            Dim lBuffer As TextBuffer = pTextView.Buffer
            CreateTextTags(lBuffer)
            
            ' Apply base styling
            Dim lCss As String = "textview { " & _
                "font-family: sans-serif; " & _
                "font-size: 12px; " & _
                "background-color: #ffffff; " & _
                "color: #333333; " & _
                "}"
            Utilities.CssHelper.ApplyCssToWidget(pTextView, lCss, 800)
            
            ' Scrolled window
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.Add(pTextView)
            
            ' Add border
            Dim lFrame As New Frame()
            lFrame.ShadowType = ShadowType.In
            lFrame.Add(lScrolled)
            
            ' Status bar
            Dim lStatusBox As New Box(Orientation.Horizontal, 4)
            lStatusBox.MarginStart = 6
            lStatusBox.MarginEnd = 6
            lStatusBox.MarginBottom = 2
            
            pStatusLabel = New Label("Ready")
            pStatusLabel.Halign = Align.Start
            lStatusBox.PackStart(pStatusLabel, True, True, 0)
            
            ' Pack components
            PackStart(lToolbar, False, False, 0)
            PackStart(lFrame, True, True, 0)
            PackStart(lStatusBox, False, False, 0)
            
            ' Load welcome content
            ShowWelcome()
        End Sub
        
        Private Sub InitializeQuickLinks()
            pQuickLinks.Add("VB.NET Language Reference", "https://learn.microsoft.com/en-us/dotnet/Visual-basic/")
            pQuickLinks.Add("GTK# documentation", "https://docs.gtk.org/gtk3/")
            pQuickLinks.Add(".NET documentation", "https://learn.microsoft.com/en-us/dotnet/")
            pQuickLinks.Add("Linux Deployment", "https://learn.microsoft.com/en-us/dotnet/core/install/linux")
            pQuickLinks.Add("VB.NET Keywords", "https://learn.microsoft.com/en-us/dotnet/Visual-basic/Language-Reference/Keywords/")
            pQuickLinks.Add("VB.NET Operators", "https://learn.microsoft.com/en-us/dotnet/Visual-basic/Language-Reference/Operators/")
            pQuickLinks.Add("GTK# Tutorial", "https://www.mono-project.com/docs/GUI/gtksharp/beginners-guide/")
        End Sub
        
        Private Sub CreateTextTags(vBuffer As TextBuffer)
            ' Create tags for formatting
            Dim lTagTable As TextTagTable = vBuffer.TagTable
            
            ' Heading tags
            Dim lH1Tag As New TextTag("h1") With {
                .Scale = Pango.Scale.XXLarge,
                .Weight = Pango.Weight.Bold,
                .PixelsAboveLines = 10,
                .PixelsBelowLines = 10
            }
            lTagTable.Add(lH1Tag)
            
            Dim lH2Tag As New TextTag("h2") With {
                .Scale = Pango.Scale.XLarge,
                .Weight = Pango.Weight.Bold,
                .PixelsAboveLines = 8,
                .PixelsBelowLines = 8
            }
            lTagTable.Add(lH2Tag)
            
            Dim lH3Tag As New TextTag("h3") With {
                .Scale = Pango.Scale.large,
                .Weight = Pango.Weight.Bold,
                .PixelsAboveLines = 6,
                .PixelsBelowLines = 6
            }
            lTagTable.Add(lH3Tag)
            
            ' Code tag
            Dim lCodeTag As New TextTag("code") With {
                .Family = "monospace",
                .Background = "#f5f5f5",
                .Foreground = "#d73a49"
            }
            lTagTable.Add(lCodeTag)
            
            ' Link tag
            Dim lLinkTag As New TextTag("link") With {
                .Foreground = "#0366d6",
                .Underline = Pango.Underline.Single
            }
            lTagTable.Add(lLinkTag)
            
            ' Bold tag
            Dim lBoldTag As New TextTag("bold") With {
                .Weight = Pango.Weight.Bold
            }
            lTagTable.Add(lBoldTag)
            
            ' Italic tag
            Dim lItalicTag As New TextTag("italic") With {
                .Style = Pango.Style.Italic
            }
            lTagTable.Add(lItalicTag)
        End Sub
        
        Private Sub ShowWelcome()
            Dim lBuffer As TextBuffer = pTextView.Buffer
            lBuffer.Clear()
            
            ' Build welcome text with formatting
            Dim lText As New System.Text.StringBuilder()
            lText.AppendLine(StringResources.Instance.GetString(StringResources.KEY_WELCOME_MESSAGE))
            
            ' Set the text
            lBuffer.Text = lText.ToString()
            
            ' Apply formatting using tags
            ApplyWelcomeFormatting(lBuffer)
            
            pStatusLabel.Text = "Welcome"
            RaiseEvent TitleChanged("Help - Welcome")
        End Sub
        
        Private Sub ApplyWelcomeFormatting(vBuffer As TextBuffer)
            Try
                ' Apply h1 to title
                Dim lStartIter As TextIter = vBuffer.GetIterAtLineOffset(0, 0)
                Dim lEndIter As TextIter = vBuffer.GetIterAtLineOffset(0, 24) ' Length of "SimpleIDE Help System"
                vBuffer.ApplyTag("h1", lStartIter, lEndIter)
                
                ' Apply h2 to welcome message
                lStartIter = vBuffer.GetIterAtLineOffset(2, 0)
                lEndIter = vBuffer.GetIterAtLineOffset(2, 39) ' Length of welcome Message
                vBuffer.ApplyTag("h2", lStartIter, lEndIter)
                
                ' Apply h3 to "Keyboard Shortcuts:"
                lStartIter = vBuffer.GetIterAtLineOffset(10, 0)
                lEndIter = vBuffer.GetIterAtLineOffset(10, 18)
                vBuffer.ApplyTag("h3", lStartIter, lEndIter)
                
                ' Apply code tag to F1
                lStartIter = vBuffer.GetIterAtLineOffset(12, 0)
                lEndIter = vBuffer.GetIterAtLineOffset(12, 2)
                vBuffer.ApplyTag("code", lStartIter, lEndIter)
                
                ' Apply bold to "Note:"
                lStartIter = vBuffer.GetIterAtLineOffset(14, 0)
                lEndIter = vBuffer.GetIterAtLineOffset(14, 5)
                vBuffer.ApplyTag("bold", lStartIter, lEndIter)
                
            Catch ex As Exception
                Console.WriteLine($"error applying welcome formatting: {ex.Message}")
            End Try
        End Sub
        
        Public Sub LoadUrl(vUrl As String)
            If String.IsNullOrEmpty(vUrl) Then Return
            
            ' Add to history
            If pHistoryIndex < pHistory.Count - 1 Then
                pHistory.RemoveRange(pHistoryIndex + 1, pHistory.Count - pHistoryIndex - 1)
            End If
            pHistory.Add(vUrl)
            pHistoryIndex = pHistory.Count - 1
            UpdateNavigationButtons()
            
            ' Start loading
            pProgressSpinner.Start()
            pStatusLabel.Text = "Loading..."
            Application.Invoke(Sub() pTextView.Buffer.Clear())
            
            Task.Run(Async Function()
                Try
                    Dim lContent As String = Await pHttpClient.GetStringAsync(vUrl)
                    Dim lTitle As String = ExtractTitle(lContent)
                    Dim lText As String = ConvertHtmlToText(lContent)
                    
                    Application.Invoke(Sub()
                        pTextView.Buffer.Text = lText
                        pProgressSpinner.Stop()
                        pStatusLabel.Text = $"loaded: {vUrl}"
                        RaiseEvent TitleChanged($"Help - {lTitle}")
                    End Sub)
                Catch ex As Exception
                    Application.Invoke(Sub()
                        ShowError($"error loading page: {ex.Message}", vUrl)
                        pProgressSpinner.Stop()
                        pStatusLabel.Text = "error"
                    End Sub)
                End Try
            End Function)
        End Sub
        
        Private Function ExtractTitle(vHtml As String) As String
            Dim lMatch As Match = Regex.Match(vHtml, "<Title>(.*?)</Title>", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
            If lMatch.Success Then
                Return System.Net.WebUtility.HtmlDecode(lMatch.Groups(1).Value.Trim())
            End If
            Return "Untitled"
        End Function
        
        Private Function ConvertHtmlToText(vHtml As String) As String
            ' This is a simplified HTML to text converter
            Dim lText As String = vHtml
            
            ' Remove script and style elements completely
            lText = Regex.Replace(lText, "<script.*?</script>", "", RegexOptions.Singleline Or RegexOptions.IgnoreCase)
            lText = Regex.Replace(lText, "<style.*?</style>", "", RegexOptions.Singleline Or RegexOptions.IgnoreCase)
            
            ' Convert headings
            lText = Regex.Replace(lText, "<h1.*?>(.*?)</h1>", Environment.NewLine & "=== $1 ===" & Environment.NewLine & Environment.NewLine, RegexOptions.Singleline Or RegexOptions.IgnoreCase)
            lText = Regex.Replace(lText, "<h2.*?>(.*?)</h2>", Environment.NewLine & "== $1 ==" & Environment.NewLine & Environment.NewLine, RegexOptions.Singleline Or RegexOptions.IgnoreCase)
            lText = Regex.Replace(lText, "<h3.*?>(.*?)</h3>", Environment.NewLine & "= $1 =" & Environment.NewLine & Environment.NewLine, RegexOptions.Singleline Or RegexOptions.IgnoreCase)
            
            ' Convert line breaks and paragraphs
            lText = Regex.Replace(lText, "<br\s*/?>", Environment.NewLine, RegexOptions.IgnoreCase)
            lText = Regex.Replace(lText, "</p>", Environment.NewLine & Environment.NewLine, RegexOptions.IgnoreCase)
            lText = Regex.Replace(lText, "<li.*?>(.*?)</li>", "• $1" & Environment.NewLine, RegexOptions.Singleline Or RegexOptions.IgnoreCase)
            
            ' Remove remaining HTML tags
            lText = Regex.Replace(lText, "<.*?>", "")
            
            ' Decode HTML entities
            lText = System.Net.WebUtility.HtmlDecode(lText)
            
            ' Clean up whitespace
            lText = Regex.Replace(lText, "[ \t]+", " ")
            lText = Regex.Replace(lText, "(\r?\n){3,}", Environment.NewLine & Environment.NewLine)
            
            Return lText.Trim()
        End Function
        
        Private Sub ShowError(vMessage As String, vUrl As String)
            Dim lText As New System.Text.StringBuilder()
            lText.AppendLine("error Loading Page")
            lText.AppendLine()
            lText.AppendLine(vMessage)
            lText.AppendLine()
            lText.AppendLine($"Url: {vUrl}")
            lText.AppendLine()
            lText.AppendLine("this simplified viewer may not support all web Content.")
            lText.AppendLine("Try opening the page in your web browser for the full experience.")
            
            pTextView.Buffer.Text = lText.ToString()
        End Sub
        
        ' Public methods for specific help topics
        Public Sub ShowVBNetHelp()
            LoadUrl("https://learn.microsoft.com/en-us/dotnet/Visual-basic/")
        End Sub
        
        Public Sub ShowGtkHelp()
            LoadUrl("https://docs.gtk.org/gtk3/")
        End Sub
        
        Public Sub ShowDotNetHelp()
            LoadUrl("https://learn.microsoft.com/en-us/dotnet/")
        End Sub
        
        Public Sub ShowLinuxHelp()
            LoadUrl("https://learn.microsoft.com/en-us/dotnet/core/install/linux")
        End Sub
        
        ' Event handlers
        Private Sub OnBackClicked(vSender As Object, vE As EventArgs)
            If pHistoryIndex > 0 Then
                pHistoryIndex -= 1
                LoadUrl(pHistory(pHistoryIndex))
            End If
        End Sub
        
        Private Sub OnForwardClicked(vSender As Object, vE As EventArgs)
            If pHistoryIndex < pHistory.Count - 1 Then
                pHistoryIndex += 1
                LoadUrl(pHistory(pHistoryIndex))
            End If
        End Sub
        
        Private Sub OnHomeClicked(vSender As Object, vE As EventArgs)
            ShowWelcome()
        End Sub
        
        Private Sub OnUrlComboChanged(vSender As Object, vE As EventArgs)
            Dim lSelectedText As String = pUrlCombo.ActiveText
            If Not String.IsNullOrEmpty(lSelectedText) AndAlso pQuickLinks.ContainsKey(lSelectedText) Then
                LoadUrl(pQuickLinks(lSelectedText))
            End If
        End Sub
        
        Private Sub UpdateNavigationButtons()
            pBackButton.Sensitive = pHistoryIndex > 0
            pForwardButton.Sensitive = pHistoryIndex < pHistory.Count - 1
        End Sub
    End Class
End Namespace
