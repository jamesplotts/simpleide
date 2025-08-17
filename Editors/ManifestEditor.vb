' Editors/ManifestEditor.vb - Application manifest editor with XML syntax highlighting
Imports Gtk
Imports System.IO
Imports System.Xml
Imports System.Text
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Editors
    Public Class ManifestEditor
        Inherits Box
        
        ' Private fields
        Private pTextView As TextView
        Private pBuffer As TextBuffer
        Private pScrolledWindow As ScrolledWindow
        Private pProjectFile As String
        Private pManifestPath As String
        Private pIsModified As Boolean = False
        Private pSettingsManager As SettingsManager
        
        ' Syntax highlighting tags
        Private pTagComment As TextTag
        Private pTagElement As TextTag
        Private pTagAttribute As TextTag
        Private pTagValue As TextTag
        Private pTagCData As TextTag
        
        ' Events
        Public Event Modified(vIsModified As Boolean)
        Public Event SaveRequested()
        
        ' Properties
        Public Property IsModified As Boolean
            Get
                Return pIsModified
            End Get
            Set(Value As Boolean)
                If pIsModified <> Value Then
                    pIsModified = Value
                    RaiseEvent Modified(pIsModified)
                End If
            End Set
        End Property
        
        Public Sub New(vParent As Window, vProjectFile As String, vSettingsManager As SettingsManager)
            MyBase.New(Orientation.Vertical, 0)
            
            pProjectFile = vProjectFile
            pSettingsManager = vSettingsManager
            
            ' Determine manifest path
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(vProjectFile)
            pManifestPath = System.IO.Path.Combine(lProjectDir, "app.manifest")
            
            BuildUI()
            LoadManifest()
        End Sub
        
        Private Sub BuildUI()
            Try
                ' Create toolbar
                Dim lToolbar As Widget = CreateToolbar()
                PackStart(lToolbar, False, False, 0)
                
                ' Create text view
                pTextView = New TextView()
                pTextView.LeftMargin = 5
                pTextView.RightMargin = 5
                pTextView.TopMargin = 5
                pTextView.BottomMargin = 5
                pTextView.WrapMode = WrapMode.None
                pTextView.Monospace = True
                
                ' Create buffer
                pBuffer = pTextView.Buffer
                
                ' Create syntax highlighting tags
                CreateSyntaxTags()
                
                ' Connect events
                AddHandler pBuffer.Changed, AddressOf OnBufferChanged
                AddHandler pTextView.KeyPressEvent, AddressOf OnKeyPress
                
                ' Create scrolled window
                pScrolledWindow = New ScrolledWindow()
                pScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                pScrolledWindow.Add(pTextView)
                
                ' Pack everything
                PackStart(pScrolledWindow, True, True, 0)
                
                ' Apply font settings
                ApplyFontSettings()
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"error building manifest Editor UI: {ex.Message}")
            End Try
        End Sub
        
        Private Function CreateToolbar() As Widget
            Dim lToolbar As New Toolbar()
            lToolbar.ToolbarStyle = ToolbarStyle.Icons
            lToolbar.IconSize = IconSize.SmallToolbar
            
            ' Save button
            Dim lSaveButton As New ToolButton(Nothing, "Save")
            lSaveButton.IconWidget = Image.NewFromIconName("document-Save", IconSize.SmallToolbar)
            lSaveButton.TooltipText = "Save Manifest"
            AddHandler lSaveButton.Clicked, AddressOf OnSave
            lToolbar.Insert(lSaveButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Format button
            Dim lFormatButton As New ToolButton(Nothing, "Format")
            lFormatButton.IconWidget = Image.NewFromIconName("format-indent-more", IconSize.SmallToolbar)
            lFormatButton.TooltipText = "Format XML"
            AddHandler lFormatButton.Clicked, AddressOf OnFormat
            lToolbar.Insert(lFormatButton, -1)
            
            ' Validate button
            Dim lValidateButton As New ToolButton(Nothing, "Validate")
            lValidateButton.IconWidget = Image.NewFromIconName("dialog-information", IconSize.SmallToolbar)
            lValidateButton.TooltipText = "Validate XML"
            AddHandler lValidateButton.Clicked, AddressOf OnValidate
            lToolbar.Insert(lValidateButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Template menu
            Dim lTemplateButton As New MenuToolButton(Nothing, "Templates")
            lTemplateButton.IconWidget = Image.NewFromIconName("document-New", IconSize.SmallToolbar)
            lTemplateButton.TooltipText = "Insert Template"
            lTemplateButton.Menu = CreateTemplateMenu()
            lToolbar.Insert(lTemplateButton, -1)
            
            Return lToolbar
        End Function
        
        Private Function CreateTemplateMenu() As Menu
            Dim lMenu As New Menu()
            
            ' Basic manifest
            Dim lBasicItem As New MenuItem("Basic Application Manifest")
            AddHandler lBasicItem.Activated, Sub() InsertTemplate(TemplateType.Basic)
            lMenu.Append(lBasicItem)
            
            ' UAC Administrator
            Dim lAdminItem As New MenuItem("Require Administrator")
            AddHandler lAdminItem.Activated, Sub() InsertTemplate(TemplateType.Administrator)
            lMenu.Append(lAdminItem)
            
            ' DPI Aware
            Dim lDpiItem As New MenuItem("DPI Aware Application")
            AddHandler lDpiItem.Activated, Sub() InsertTemplate(TemplateType.DpiAware)
            lMenu.Append(lDpiItem)
            
            ' Windows 10/11 Compatible
            Dim lWin10Item As New MenuItem("Windows 10/11 Compatible")
            AddHandler lWin10Item.Activated, Sub() InsertTemplate(TemplateType.Windows10)
            lMenu.Append(lWin10Item)
            
            lMenu.ShowAll()
            Return lMenu
        End Function
        
        Private Sub CreateSyntaxTags()
            ' FIXED: Use TagTable.Add instead of Buffer.CreateTag
            ' Comment tag
            pTagComment = New TextTag("comment")
            pTagComment.Foreground = "#008000"
            pBuffer.TagTable.Add(pTagComment)
            
            ' Element tag
            pTagElement = New TextTag("element")
            pTagElement.Foreground = "#0000FF"
            pBuffer.TagTable.Add(pTagElement)
            
            ' Attribute tag
            pTagAttribute = New TextTag("attribute")
            pTagAttribute.Foreground = "#FF0000"
            pBuffer.TagTable.Add(pTagAttribute)
            
            ' Value tag
            pTagValue = New TextTag("Value")
            pTagValue.Foreground = "#800080"
            pBuffer.TagTable.Add(pTagValue)
            
            ' CDATA tag
            pTagCData = New TextTag("cdata")
            pTagCData.Foreground = "#808080"
            pBuffer.TagTable.Add(pTagCData)
        End Sub
        
        Private Sub LoadManifest()
            Try
                If File.Exists(pManifestPath) Then
                    ' Load existing manifest
                    pBuffer.Text = File.ReadAllText(pManifestPath)
                Else
                    ' Create default manifest
                    pBuffer.Text = GetDefaultManifest()
                End If
                
                ' Apply syntax highlighting
                ApplySyntaxHighlighting()
                
                ' Reset modified flag
                IsModified = False
                
            Catch ex As Exception
                Console.WriteLine($"error loading manifest: {ex.Message}")
                pBuffer.Text = GetDefaultManifest()
            End Try
        End Sub
        
        Private Function GetDefaultManifest() As String
            Return "<?xml Version=""1.0"" Encoding=""utf-8""?>" & Environment.NewLine & _
                   "<assembly manifestVersion=""1.0"" xmlns=""urn:schemas-microsoft-com:asm.v1"">" & Environment.NewLine & _
                   "  <assemblyIdentity Version=""1.0.0.0"" Name=""MyApplication.app""/>" & Environment.NewLine & _
                   "  <trustInfo xmlns=""urn:schemas-microsoft-com:asm.v2"">" & Environment.NewLine & _
                   "    <security>" & Environment.NewLine & _
                   "      <requestedPrivileges xmlns=""urn:schemas-microsoft-com:asm.v3"">" & Environment.NewLine & _
                   "        <requestedExecutionLevel Level=""asInvoker"" uiAccess=""false"" />" & Environment.NewLine & _
                   "      </requestedPrivileges>" & Environment.NewLine & _
                   "    </security>" & Environment.NewLine & _
                   "  </trustInfo>" & Environment.NewLine & _
                   "</assembly>"
        End Function
        
        Private Sub OnBufferChanged(vSender As Object, vE As EventArgs)
            IsModified = True
            
            ' Reapply syntax highlighting
            GLib.Idle.Add(Function()
                              ApplySyntaxHighlighting()
                              Return False
                          End Function)
        End Sub
        
        Private Sub OnKeyPress(vSender As Object, vE As KeyPressEventArgs)
            ' Handle Ctrl+S for save
            If (vE.Event.State And Gdk.ModifierType.ControlMask) = Gdk.ModifierType.ControlMask AndAlso
               vE.Event.key = CType(115, Gdk.key) Then ' FIXED: Use keyval 115 for 's'
                OnSave(Nothing, Nothing)
                vE.RetVal = True
            End If
        End Sub
        
        Private Sub OnSave(vSender As Object, vE As EventArgs)
            Try
                SaveManifest()
                IsModified = False
                RaiseEvent SaveRequested()
            Catch ex As Exception
                ShowErrorDialog($"Failed to Save manifest: {ex.Message}")
            End Try
        End Sub
        
        Public Sub SaveManifest()
            Try
                ' Ensure directory exists
                Dim lDir As String = System.IO.Path.GetDirectoryName(pManifestPath)
                If Not Directory.Exists(lDir) Then
                    Directory.CreateDirectory(lDir)
                End If
                
                ' Save the file
                File.WriteAllText(pManifestPath, pBuffer.Text)
                
            Catch ex As Exception
                Throw
            End Try
        End Sub
        
        Private Sub OnFormat(vSender As Object, vE As EventArgs)
            Try
                ' Parse and reformat XML
                Dim lDoc As New XmlDocument()
                lDoc.LoadXml(pBuffer.Text)
                
                ' Format with indentation
                Dim lSettings As New XmlWriterSettings()
                lSettings.Indent = True
                lSettings.IndentChars = "  "
                lSettings.NewLineChars = Environment.NewLine
                lSettings.NewLineHandling = NewLineHandling.Replace
                
                Using lStringWriter As New StringWriter()
                    Using lXmlWriter As XmlWriter = XmlWriter.Create(lStringWriter, lSettings)
                        lDoc.Save(lXmlWriter)
                    End Using
                    pBuffer.Text = lStringWriter.ToString()
                End Using
                
                ' Reapply syntax highlighting
                ApplySyntaxHighlighting()
                
            Catch ex As Exception
                ShowErrorDialog($"Invalid XML: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnValidate(vSender As Object, vE As EventArgs)
            Try
                ' Validate XML
                Dim lDoc As New XmlDocument()
                lDoc.LoadXml(pBuffer.Text)
                
                ShowInfoDialog("XML is valid!")
                
            Catch ex As Exception
                ShowErrorDialog($"XML validation failed: {ex.Message}")
            End Try
        End Sub
        
        Private Enum TemplateType
            Basic
            Administrator
            DpiAware
            Windows10
        End Enum
        
        Private Sub InsertTemplate(vType As TemplateType)
            Dim lTemplate As String = ""
            
            Select Case vType
                Case TemplateType.Basic
                    lTemplate = GetDefaultManifest()
                    
                Case TemplateType.Administrator
                    lTemplate = "<?xml Version=""1.0"" Encoding=""utf-8""?>" & Environment.NewLine & _
                               "<assembly manifestVersion=""1.0"" xmlns=""urn:schemas-microsoft-com:asm.v1"">" & Environment.NewLine & _
                               "  <assemblyIdentity Version=""1.0.0.0"" Name=""MyApplication.app""/>" & Environment.NewLine & _
                               "  <trustInfo xmlns=""urn:schemas-microsoft-com:asm.v2"">" & Environment.NewLine & _
                               "    <security>" & Environment.NewLine & _
                               "      <requestedPrivileges xmlns=""urn:schemas-microsoft-com:asm.v3"">" & Environment.NewLine & _
                               "        <requestedExecutionLevel Level=""asInvoker"" uiAccess=""false"" />" & Environment.NewLine & _
                               "      </requestedPrivileges>" & Environment.NewLine & _
                               "    </security>" & Environment.NewLine & _
                               "  </trustInfo>" & Environment.NewLine & _
                               "</assembly>"
                               
                Case TemplateType.Windows10
                    lTemplate = "<?xml Version=""1.0"" Encoding=""utf-8""?>" & Environment.NewLine & _
                               "<assembly manifestVersion=""1.0"" xmlns=""urn:schemas-microsoft-com:asm.v1"">" & Environment.NewLine & _
                               "  <assemblyIdentity Version=""1.0.0.0"" Name=""MyApplication.app""/>" & Environment.NewLine & _
                               "  <compatibility xmlns=""urn:schemas-microsoft-com:compatibility.v1"">" & Environment.NewLine & _
                               "    <application>" & Environment.NewLine & _
                               "      <!-- Windows Vista -->" & Environment.NewLine & _
                               "      <supportedOS Id=""{e2011457-1546-43c5-a5fe-008deee3d3f0}"" />" & Environment.NewLine & _
                               "      <!-- Windows 7 -->" & Environment.NewLine & _
                               "      <supportedOS Id=""{35138b9a-5d96-4fbd-8e2d-a2440225f93a}"" />" & Environment.NewLine & _
                               "      <!-- Windows 8 -->" & Environment.NewLine & _
                               "      <supportedOS Id=""{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}"" />" & Environment.NewLine & _
                               "      <!-- Windows 8.1 -->" & Environment.NewLine & _
                               "      <supportedOS Id=""{1f676c76-80e1-4239-95bb-83d0f6d0da78}"" />" & Environment.NewLine & _
                               "      <!-- Windows 10 -->" & Environment.NewLine & _
                               "      <supportedOS Id=""{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"" />" & Environment.NewLine & _
                               "    </application>" & Environment.NewLine & _
                               "  </compatibility>" & Environment.NewLine & _
                               "  <trustInfo xmlns=""urn:schemas-microsoft-com:asm.v2"">" & Environment.NewLine & _
                               "    <security>" & Environment.NewLine & _
                               "      <requestedPrivileges xmlns=""urn:schemas-microsoft-com:asm.v3"">" & Environment.NewLine & _
                               "        <requestedExecutionLevel Level=""asInvoker"" uiAccess=""false"" />" & Environment.NewLine & _
                               "      </requestedPrivileges>" & Environment.NewLine & _
                               "    </security>" & Environment.NewLine & _
                               "  </trustInfo>" & Environment.NewLine & _
                               "</assembly>"
            End Select
            
            ' Replace buffer content
            pBuffer.Text = lTemplate
            
            ' Apply syntax highlighting
            ApplySyntaxHighlighting()
        End Sub
        
        Private Sub ApplySyntaxHighlighting()
            Try
                ' Remove all tags first
                Dim lStart As TextIter = pBuffer.StartIter
                Dim lEnd As TextIter = pBuffer.EndIter
                pBuffer.RemoveAllTags(lStart, lEnd)
                
                ' Get text
                Dim lText As String = pBuffer.Text
                Dim lLength As Integer = lText.Length
                Dim i As Integer = 0
                
                While i < lLength
                    If i + 3 < lLength AndAlso lText.Substring(i, 4) = "<!--" Then
                        ' Comment
                        Dim lCommentEnd As Integer = lText.IndexOf("-->", i + 4)
                        If lCommentEnd > 0 Then
                            lCommentEnd += 3
                            ApplyTag(pTagComment, i, lCommentEnd)
                            i = lCommentEnd
                            Continue While
                        End If
                    ElseIf i + 8 < lLength AndAlso lText.Substring(i, 9) = "<![CDATA[" Then
                        ' CDATA section
                        Dim lCDataEnd As Integer = lText.IndexOf("]]>", i + 9)
                        If lCDataEnd > 0 Then
                            lCDataEnd += 3
                            ApplyTag(pTagCData, i, lCDataEnd)
                            i = lCDataEnd
                            Continue While
                        End If
                    ElseIf lText(i) = "<"c Then
                        ' XML tag
                        Dim lTagEnd As Integer = lText.IndexOf(">"c, i + 1)
                        If lTagEnd > 0 Then
                            ' Apply element tag to < and >
                            ApplyTag(pTagElement, i, i + 1)
                            ApplyTag(pTagElement, lTagEnd, lTagEnd + 1)
                            
                            ' Find element name
                            Dim lNameStart As Integer = i + 1
                            If lText(lNameStart) = "/"c Then lNameStart += 1
                            
                            Dim lNameEnd As Integer = lNameStart
                            While lNameEnd < lTagEnd AndAlso Not Char.IsWhiteSpace(lText(lNameEnd)) AndAlso lText(lNameEnd) <> "/"c
                                lNameEnd += 1
                            End While
                            
                            If lNameEnd > lNameStart Then
                                ApplyTag(pTagElement, lNameStart, lNameEnd)
                            End If
                            
                            ' Parse attributes
                            Dim lPos As Integer = lNameEnd
                            While lPos < lTagEnd
                                ' Skip whitespace
                                While lPos < lTagEnd AndAlso Char.IsWhiteSpace(lText(lPos))
                                    lPos += 1
                                End While
                                
                                If lPos >= lTagEnd OrElse lText(lPos) = "/"c Then Exit While
                                
                                ' Attribute name
                                Dim lAttrStart As Integer = lPos
                                While lPos < lTagEnd AndAlso lText(lPos) <> "="c AndAlso Not Char.IsWhiteSpace(lText(lPos))
                                    lPos += 1
                                End While
                                
                                If lPos > lAttrStart Then
                                    ApplyTag(pTagAttribute, lAttrStart, lPos)
                                End If
                                
                                ' Skip to = sign
                                While lPos < lTagEnd AndAlso lText(lPos) <> "="c
                                    lPos += 1
                                End While
                                
                                If lPos < lTagEnd AndAlso lText(lPos) = "="c Then
                                    lPos += 1
                                    
                                    ' Skip whitespace
                                    While lPos < lTagEnd AndAlso Char.IsWhiteSpace(lText(lPos))
                                        lPos += 1
                                    End While
                                    
                                    ' Attribute value
                                    If lPos < lTagEnd AndAlso lText(lPos) = """"c Then
                                        Dim lValueStart As Integer = lPos
                                        lPos += 1
                                        While lPos < lTagEnd AndAlso lText(lPos) <> """"c
                                            lPos += 1
                                        End While
                                        If lPos < lTagEnd Then
                                            lPos += 1
                                            ApplyTag(pTagValue, lValueStart, lPos)
                                        End If
                                    End If
                                End If
                            End While
                            
                            i = lTagEnd + 1
                            Continue While
                        End If
                    End If
                    
                    i += 1
                End While
                
            Catch ex As Exception
                Console.WriteLine($"error applying syntax highlighting: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ApplyTag(vTag As TextTag, vStartOffset As Integer, vEndOffset As Integer)
            Try
                Dim lStartIter As TextIter = pBuffer.GetIterAtOffset(vStartOffset)
                Dim lEndIter As TextIter = pBuffer.GetIterAtOffset(vEndOffset)
                pBuffer.ApplyTag(vTag, lStartIter, lEndIter)
            Catch ex As Exception
                ' Ignore errors in tag application
            End Try
        End Sub
        
        Private Sub ApplyFontSettings()
            Try
                ' Apply font from settings
                If pSettingsManager IsNot Nothing Then
                    Dim lFontDesc As String = pSettingsManager.EditorFont
                    If Not String.IsNullOrEmpty(lFontDesc) Then
                        ' FIXED: Use GenerateTextViewFontCss and ApplyCssToWidget instead
                        Dim lCss As String = CssHelper.GenerateTextViewFontCss(lFontDesc)
                        CssHelper.ApplyCssToWidget(pTextView, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"error applying font settings: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ShowErrorDialog(vMessage As String)
            Dim lDialog As New MessageDialog(Nothing, DialogFlags.Modal,
                                           MessageType.Error, ButtonsType.Ok,
                                           vMessage)
            lDialog.Run()
            lDialog.Destroy()
        End Sub
        
        Private Sub ShowInfoDialog(vMessage As String)
            Dim lDialog As New MessageDialog(Nothing, DialogFlags.Modal,
                                           MessageType.Info, ButtonsType.Ok,
                                           vMessage)
            lDialog.Run()
            lDialog.Destroy()
        End Sub
    End Class
End Namespace 