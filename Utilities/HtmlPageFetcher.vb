' Utilities/HtmlPageFetcher.vb - fetches an HTML page plus every stylesheet/image it
' references, entirely via managed HttpClient, before any native litehtml code runs.
'
' Deliberately NOT a live/streaming/delegate-callback bridge into the native shim - see
' the plan's Phase 5 rationale: litehtml has no async hook to resume layout once it starts,
' so a callback crossing the P/Invoke boundary mid-layout would mean either freezing the
' GTK thread on every image fetch or building real cross-thread handoff plumbing, neither
' of which is justified for a documentation viewer. Fetching everything up front with plain
' async/await here, then handing the finished result to LiteHtmlDocumentHandle in one
' shot, is simpler and has no GTK-thread implications at all - this class never touches GTK.
Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Namespace Utilities

    ''' <summary>
    ''' The result of fetching a page and its referenced resources - everything
    ''' LiteHtmlDocumentHandle needs to create and fully lay out a document with no
    ''' further network I/O
    ''' </summary>
    Public Class HtmlPageFetchResult

        ''' <summary>Gets or sets whether the fetch succeeded</summary>
        Public Property Success As Boolean = False

        ''' <summary>Gets or sets the error message if Success is False</summary>
        Public Property ErrorMessage As String = ""

        ''' <summary>Gets or sets the page's raw HTML</summary>
        Public Property Html As String = ""

        ''' <summary>Gets or sets the page's resolved absolute URL (used as the base for
        ''' relative link/image/stylesheet resolution)</summary>
        Public Property BaseUrl As String = ""

        ''' <summary>Gets or sets every successfully-fetched stylesheet/image, keyed by
        ''' resolved absolute URL, ready to hand to LiteHtmlDocumentHandle.AddResource</summary>
        Public Property Resources As New Dictionary(Of String, Byte())

    End Class

    ''' <summary>
    ''' Fetches documentation pages for CustomDrawHtmlView - the page itself plus every
    ''' `&lt;link rel=stylesheet&gt;`/`&lt;img&gt;` resource it references, all via
    ''' HttpClient, before any native rendering happens
    ''' </summary>
    Public Class HtmlPageFetcher

        ' Shared across all fetches (and ideally the app's lifetime) rather than one
        ' HttpClient per call, matching the standard .NET guidance against socket
        ' exhaustion from repeated short-lived instances
        Private Shared ReadOnly pHttpClient As HttpClient = CreateHttpClient()

        ''' <summary>
        ''' Builds the shared HttpClient with a normal desktop-browser User-Agent - without
        ''' one, sites like learn.microsoft.com treat the request as an unrecognized/legacy
        ''' client and inject a "this browser is no longer supported" banner into the page,
        ''' even though litehtml renders the actual content just fine
        ''' </summary>
        Private Shared Function CreateHttpClient() As HttpClient
            Dim lClient As New HttpClient()
            lClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
            Return lClient
        End Function

        Private Shared ReadOnly pStylesheetLinkPattern As New Regex(
            "<link\b[^>]*>", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
        Private Shared ReadOnly pImagePattern As New Regex(
            "<img\b[^>]*>", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
        Private Shared ReadOnly pRelAttrPattern As New Regex(
            "\brel\s*=\s*[""']?([^"" '>]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
        Private Shared ReadOnly pHrefAttrPattern As New Regex(
            "\bhref\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
        Private Shared ReadOnly pSrcAttrPattern As New Regex(
            "\bsrc\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase Or RegexOptions.Compiled)

        ''' <summary>
        ''' Fetches vUrl's HTML plus every stylesheet/image it references. Never throws -
        ''' check the returned result's Success/ErrorMessage instead
        ''' </summary>
        ''' <param name="vUrl">The page URL to fetch</param>
        ''' <returns>A result with the HTML and every successfully-fetched resource</returns>
        Public Async Function FetchPageAsync(vUrl As String) As Task(Of HtmlPageFetchResult)
            Dim lResult As New HtmlPageFetchResult()
            Try
                Dim lPageUri As New Uri(vUrl)
                Dim lHtml As String = Await pHttpClient.GetStringAsync(lPageUri)

                lResult.Html = lHtml
                lResult.BaseUrl = lPageUri.AbsoluteUri

                Dim lResourceUrls As List(Of String) = ExtractResourceUrls(lHtml, lPageUri)
                If lResourceUrls.Count > 0 Then
                    Dim lFetchTasks As New List(Of Task(Of KeyValuePair(Of String, Byte())?))
                    for each lResourceUrl in lResourceUrls
                        lFetchTasks.Add(FetchResourceAsync(lResourceUrl))
                    Next

                    Dim lFetched() As KeyValuePair(Of String, Byte())? = Await Task.WhenAll(lFetchTasks)
                    for each lEntry in lFetched
                        If lEntry.HasValue Then
                            lResult.Resources(lEntry.Value.Key) = lEntry.Value.Value
                        End If
                    Next
                End If

                lResult.Success = True

            Catch ex As Exception
                Console.WriteLine($"HtmlPageFetcher.FetchPageAsync error: {ex.Message}")
                lResult.Success = False
                lResult.ErrorMessage = ex.Message
            End Try

            Return lResult
        End Function

        ''' <summary>
        ''' Fetches a single resource's raw bytes, returning Nothing (not throwing) on
        ''' failure so one bad image/stylesheet doesn't fail the whole page
        ''' </summary>
        Private Async Function FetchResourceAsync(vUrl As String) As Task(Of KeyValuePair(Of String, Byte())?)
            Try
                Dim lBytes As Byte() = Await pHttpClient.GetByteArrayAsync(vUrl)
                Return New KeyValuePair(Of String, Byte())(vUrl, lBytes)
            Catch ex As Exception
                Console.WriteLine($"HtmlPageFetcher.FetchResourceAsync error ({vUrl}): {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Pulls every `&lt;link rel=stylesheet href=...&gt;` and `&lt;img src=...&gt;`
        ''' URL out of raw HTML and resolves each against vBaseUri - deliberately a simple
        ''' regex prescan, not a full HTML parse (litehtml itself is the authoritative
        ''' parser; this only needs to know what to pre-fetch before creating the document)
        ''' </summary>
        Private Function ExtractResourceUrls(vHtml As String, vBaseUri As Uri) As List(Of String)
            Dim lUrls As New List(Of String)
            Dim lSeen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            Try
                for each lMatch As Match in pStylesheetLinkPattern.Matches(vHtml)
                    Dim lTag As String = lMatch.Value
                    Dim lRelMatch As Match = pRelAttrPattern.Match(lTag)
                    If Not lRelMatch.Success OrElse Not lRelMatch.Groups(1).Value.Equals("stylesheet", StringComparison.OrdinalIgnoreCase) Then
                        Continue for
                    End If

                    Dim lHrefMatch As Match = pHrefAttrPattern.Match(lTag)
                    If lHrefMatch.Success Then
                        AddResolvedUrl(lHrefMatch.Groups(1).Value, vBaseUri, lUrls, lSeen)
                    End If
                Next

                for each lMatch As Match in pImagePattern.Matches(vHtml)
                    Dim lSrcMatch As Match = pSrcAttrPattern.Match(lMatch.Value)
                    If lSrcMatch.Success Then
                        AddResolvedUrl(lSrcMatch.Groups(1).Value, vBaseUri, lUrls, lSeen)
                    End If
                Next

            Catch ex As Exception
                Console.WriteLine($"HtmlPageFetcher.ExtractResourceUrls error: {ex.Message}")
            End Try

            Return lUrls
        End Function

        ''' <summary>
        ''' Resolves a possibly-relative URL against the page's base URI (skipping
        ''' data: URIs, which need no fetch) and adds it if not already seen
        ''' </summary>
        Private Sub AddResolvedUrl(vRawUrl As String, vBaseUri As Uri, vUrls As List(Of String), vSeen As HashSet(Of String))
            Try
                If String.IsNullOrWhiteSpace(vRawUrl) Then Return
                If vRawUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) Then Return

                Dim lResolved As Uri = Nothing
                If Not Uri.TryCreate(vBaseUri, vRawUrl, lResolved) Then Return

                Dim lAbsolute As String = lResolved.AbsoluteUri
                If vSeen.Add(lAbsolute) Then
                    vUrls.Add(lAbsolute)
                End If

            Catch ex As Exception
                Console.WriteLine($"HtmlPageFetcher.AddResolvedUrl error ({vRawUrl}): {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
