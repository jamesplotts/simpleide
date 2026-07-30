' Utilities/CssHelper.vb
Imports Gtk

Namespace Utilities
    Public Module CssHelper
        ' CSS Priority Constants
        Public Const STYLE_PROVIDER_PRIORITY_FALLBACK As UInteger = 1
        Public Const STYLE_PROVIDER_PRIORITY_THEME As UInteger = 200
        Public Const STYLE_PROVIDER_PRIORITY_SETTINGS As UInteger = 400
        Public Const STYLE_PROVIDER_PRIORITY_APPLICATION As UInteger = 600
        Public Const STYLE_PROVIDER_PRIORITY_USER As UInteger = 800
        
        ' Generate CSS for TextView font
        Public Function GenerateTextViewFontCss(vFontString As String) As String
            If String.IsNullOrEmpty(vFontString) Then Return ""
            
            Dim lFamily As String = ""
            Dim lSize As String = ""
            ParseFontString(vFontString, lFamily, lSize)
            
            If String.IsNullOrEmpty(lFamily) Then Return ""
            
            ' Apply font to both textview and text elements for better compatibility
            Dim lCss As String = $"textview {{ font-family: {lFamily};"
            If Not String.IsNullOrEmpty(lSize) Then
                lCss &= $" font-size: {lSize};"
            End If
            lCss &= " }" & Environment.NewLine
            
            ' Also apply to text element within textview
            lCss &= $"textview Text {{ font-family: {lFamily};"
            If Not String.IsNullOrEmpty(lSize) Then
                lCss &= $" font-size: {lSize};"
            End If
            lCss &= " }"
            
            Return lCss
        End Function
        
        ' Generate CSS for TextView background color
        Public Function GenerateTextViewBackgroundCss(vHexColor As String) As String
            If String.IsNullOrEmpty(vHexColor) Then Return ""
            
            Return $"textview {{ background-color: {vHexColor}; }}"
        End Function
        
        ' Generate CSS for TextView text color
        Public Function GenerateTextViewTextColorCss(vHexColor As String) As String
            If String.IsNullOrEmpty(vHexColor) Then Return ""
            
            Return $"textview Text {{ color: {vHexColor}; }}"
        End Function
        
        ' Generate CSS for TextView selection color
        Public Function GenerateTextViewSelectionCss(vHexColor As String) As String
            If String.IsNullOrEmpty(vHexColor) Then Return ""
            
            Return $"textview Text selection {{ background-color: {vHexColor}; }}"
        End Function
        
        ' Generate CSS for TextView cursor color and width
        Public Function GenerateTextViewCursorCss(vHexColor As String, Optional vWidth As Integer = 2) As String
            If String.IsNullOrEmpty(vHexColor) Then Return ""
            
            ' Set both caret color and make it wider for better visibility
            Return $"textview {{ caret-color: {vHexColor}; }}" & Environment.NewLine & _
                   $"textview Text {{ caret-color: {vHexColor}; }}" & Environment.NewLine & _
                   $"textview {{ -GtkTextView-cursor-width: {vWidth}; }}"
        End Function
        
        ' Combined CSS generation for TextView with all properties
        Public Function GenerateTextViewCss(vFont As String, vBackgroundColor As String, vTextColor As String, vSelectionColor As String) As String
            Dim lCss As String = ""
            
            ' Add font CSS
            If Not String.IsNullOrEmpty(vFont) Then
                lCss &= GenerateTextViewFontCss(vFont) & Environment.NewLine
            End If
            
            ' Add background color CSS
            If Not String.IsNullOrEmpty(vBackgroundColor) Then
                lCss &= GenerateTextViewBackgroundCss(vBackgroundColor) & Environment.NewLine
            End If
            
            ' Add text color CSS
            If Not String.IsNullOrEmpty(vTextColor) Then
                lCss &= GenerateTextViewTextColorCss(vTextColor) & Environment.NewLine
            End If
            
            ' Add selection color CSS
            If Not String.IsNullOrEmpty(vSelectionColor) Then
                lCss &= GenerateTextViewSelectionCss(vSelectionColor) & Environment.NewLine
            End If
            
            Return lCss.TrimEnd()
        End Function
        
        ' Apply CSS to a widget using a CSS provider
        Public Sub ApplyCssToWidget(vWidget As Widget, vCss As String, vPriority As UInteger)
            If String.IsNullOrEmpty(vCss) Then Return
            
            Try
                Dim lCssProvider As New CssProvider()
                lCssProvider.LoadFromData(vCss)
                vWidget.StyleContext.AddProvider(lCssProvider, vPriority)
            Catch ex As Exception
                Console.WriteLine($"error applying CSS to Widget: {ex.Message}")
            End Try
        End Sub
        
        ' Apply CSS globally to all widgets
        Public Sub ApplyCssGlobally(vCss As String, vPriority As UInteger)
            If String.IsNullOrEmpty(vCss) Then Return
            
            Try
                Dim lCssProvider As New CssProvider()
                lCssProvider.LoadFromData(vCss)
                StyleContext.AddProviderForScreen(
                    Gdk.Screen.Default,
                    lCssProvider,
                    vPriority
                )
            Catch ex As Exception
                Console.WriteLine($"error applying global CSS: {ex.Message}")
            End Try
        End Sub
        
        ' Remove CSS provider from screen (for updating global styles)
        Public Sub RemoveGlobalCssProvider(vProvider As CssProvider)
            If vProvider Is Nothing Then Return
            
            Try
                StyleContext.RemoveProviderForScreen(
                    Gdk.Screen.Default,
                    vProvider
                )
            Catch ex As Exception
                Console.WriteLine($"error removing global CSS provider: {ex.Message}")
            End Try
        End Sub
        
        ' NEW METHOD: Add CSS class to widget (convenience method)
        ' This method adds a CSS class by applying CSS with the class selector
        Public Sub AddClass(vWidget As Widget, vClassName As String)
            If vWidget Is Nothing OrElse String.IsNullOrEmpty(vClassName) Then Return
            
            Try
                ' Add the CSS class to the widget's style context
                vWidget.StyleContext.AddClass(vClassName)
            Catch ex As Exception
                Console.WriteLine($"error adding CSS class '{vClassName}' to Widget: {ex.Message}")
            End Try
        End Sub
        
        ' NEW METHOD: Remove CSS class from widget
        Public Sub RemoveClass(vWidget As Widget, vClassName As String)
            If vWidget Is Nothing OrElse String.IsNullOrEmpty(vClassName) Then Return
            
            Try
                ' Remove the CSS class from the widget's style context
                vWidget.StyleContext.RemoveClass(vClassName)
            Catch ex As Exception
                Console.WriteLine($"error removing CSS class '{vClassName}' from Widget: {ex.Message}")
            End Try
        End Sub
        
        ' NEW METHOD: Apply CSS class with specific styling
        ' This combines adding a class and applying CSS rules for that class
        Public Sub ApplyClassWithCss(vWidget As Widget, vClassName As String, vCssRules As String, vPriority As UInteger)
            If vWidget Is Nothing OrElse String.IsNullOrEmpty(vClassName) OrElse String.IsNullOrEmpty(vCssRules) Then Return
            
            Try
                ' Add the class to the widget
                AddClass(vWidget, vClassName)
                
                ' Create CSS with class selector
                Dim lCss As String = $".{vClassName} {{ {vCssRules} }}"
                
                ' Apply the CSS
                ApplyCssToWidget(vWidget, lCss, vPriority)
            Catch ex As Exception
                Console.WriteLine($"error applying class '{vClassName}' with CSS: {ex.Message}")
            End Try
        End Sub
        
        ' Parse font string into family and size
        Public Sub ParseFontString(vFontString As String, ByRef vFamily As String, ByRef vSize As String)
            vFamily = ""
            vSize = ""
            
            If String.IsNullOrEmpty(vFontString) Then Return
            
            Try
                Dim lParts() As String = vFontString.Split(" "c)
                If lParts.Length >= 2 Then
                    vSize = lParts(lParts.Length - 1)
                    vFamily = String.Join(" ", lParts.Take(lParts.Length - 1))
                End If
            Catch ex As Exception
                Console.WriteLine($"error parsing font string '{vFontString}': {ex.Message}")
            End Try
        End Sub
        
        ' Convert Gdk.RGBA to hex color string
        Public Function RgbaToHex(vColor As Gdk.RGBA) As String
            Return String.Format("#{0:X2}{1:X2}{2:X2}", 
                                CInt(vColor.Red * 255),
                                CInt(vColor.Green * 255),
                                CInt(vColor.Blue * 255))
        End Function
        
        ' Parse hex color string to Gdk.RGBA
        Public Function HexToRgba(vHexColor As String) As Gdk.RGBA?
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHexColor) Then
                    Return lColor
                End If
            Catch ex As Exception
                Console.WriteLine($"error parsing hex Color '{vHexColor}': {ex.Message}")
            End Try
            Return Nothing
        End Function
    End Module
End Namespace
