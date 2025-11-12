' CustomDrawNotebook.Theme.vb - Theme integration for custom notebook
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    Partial Public Class CustomDrawNotebook
        Inherits Box
        Implements ICustomNotebook
        
        Private pThemeManager As ThemeManager
        
        ''' <summary>
        ''' Background color for the editor/content area
        ''' </summary>
        Public EditorBackground As Gdk.RGBA
        
        ''' <summary>
        ''' Color for inactive tabs
        ''' </summary>
        Public TabInactive As Gdk.RGBA
        
        ''' <summary>
        ''' Color for hovered tabs
        ''' </summary>
        Public TabHover As Gdk.RGBA
        
        ''' <summary>
        ''' Accent color for special states (dragging, etc.)
        ''' </summary>
        Public Sub Accent(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                
                ' Subscribe to theme change events
                If pThemeManager IsNot Nothing Then
                    AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                
                ' Apply current theme
                ApplyTheme()
                
            Catch ex As Exception
                Console.WriteLine($"SetThemeManager error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles theme change events
        ''' </summary>
        Private Sub OnThemeChanged(vTheme As EditorTheme)
            Try
                ApplyTheme()
                
            Catch ex As Exception
                Console.WriteLine($"OnThemeChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies the current theme colors
        ''' </summary>
        Public Sub ApplyTheme()
            Try
                If pThemeManager Is Nothing Then
                    LoadDefaultTheme()
                    Return
                End If
                
                ' Get current theme
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                If lTheme Is Nothing Then
                    LoadDefaultTheme()
                    Return
                End If
                
                ' Map theme colors to our tab colors
                pThemeColors = New ThemeColors()
                
                ' Background colors
                pThemeColors.Background = ParseColor(lTheme.BackgroundColor)
                pThemeColors.ActiveTab = ParseColor(lTheme.CurrentLineColor)
                pThemeColors.InactiveTab = DarkenColor(ParseColor(lTheme.BackgroundColor), 0.05)
                pThemeColors.HoverTab = LightenColor(ParseColor(lTheme.CurrentLineColor), 0.1)
                
                ' Text colors
                pThemeColors.Text = ParseColor(lTheme.ForegroundColor)
                ' Make inactive text 50% towards background color
                Dim lTextColor = ParseColor(lTheme.ForegroundColor)
                Dim lBgColor = ParseColor(lTheme.BackgroundColor)
                pThemeColors.TextInactive = New Gdk.RGBA() with {
                  .Red = (lTextColor.Red + lBgColor.Red) / 2,
                  .Green = (lTextColor.Green + lBgColor.Green) / 2,
                  .Blue = (lTextColor.Blue + lBgColor.Blue) / 2,
                  .Alpha = lTextColor.Alpha
                }


                
                ' Border colors
                pThemeColors.Border = DarkenColor(ParseColor(lTheme.BackgroundColor), 0.2)
                
                ' Special colors
                pThemeColors.ModifiedIndicator = ParseColor(lTheme.ErrorColor)
                pThemeColors.CloseButton = DarkenColor(ParseColor(lTheme.ForegroundColor), 0.3)
                pThemeColors.CloseButtonHover = ParseColor(lTheme.ErrorColor)
                
                ' Apply CSS for buttons
                ApplyButtonStyles()
                
                ' Redraw
                pTabBar.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyTheme error: {ex.Message}")
                ApplyTheme()
            End Try
        End Sub        
        
        ''' <summary>
        ''' Loads default theme colors
        ''' </summary>
        Private Sub LoadDefaultTheme()
            Try
                pThemeColors = New ThemeColors() with {
                    .Background = New Gdk.RGBA() with {.Red = 0.95, .Green = 0.95, .Blue = 0.95, .Alpha = 1},
                    .ActiveTab = New Gdk.RGBA() with {.Red = 1, .Green = 1, .Blue = 1, .Alpha = 1},
                    .InactiveTab = New Gdk.RGBA() with {.Red = 0.9, .Green = 0.9, .Blue = 0.9, .Alpha = 1},
                    .HoverTab = New Gdk.RGBA() with {.Red = 0.97, .Green = 0.97, .Blue = 0.97, .Alpha = 1},
                    .Text = New Gdk.RGBA() with {.Red = 0.2, .Green = 0.2, .Blue = 0.2, .Alpha = 1},
                    .Border = New Gdk.RGBA() with {.Red = 0.7, .Green = 0.7, .Blue = 0.7, .Alpha = 1},
                    .ModifiedIndicator = New Gdk.RGBA() with {.Red = 0.8, .Green = 0.2, .Blue = 0.2, .Alpha = 1},
                    .CloseButton = New Gdk.RGBA() with {.Red = 0.5, .Green = 0.5, .Blue = 0.5, .Alpha = 1},
                    .CloseButtonHover = New Gdk.RGBA() with {.Red = 0.8, .Green = 0.2, .Blue = 0.2, .Alpha = 1}
                }
                
                pTabBar.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"LoadDefaultTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies CSS styles to navigation buttons
        ''' </summary>
        Private Sub ApplyButtonStyles()
            Try
                Dim lCss As String = $"
                    button {{
                        background: {ColorToHex(pThemeColors.Background)};
                        color: {ColorToHex(pThemeColors.Text)};
                        border: 1px solid {ColorToHex(pThemeColors.Border)};
                        padding: 2px;
                        min-width: 24px;
                        min-height: 24px;
                    }}
                    button:hover {{
                        background: {ColorToHex(pThemeColors.HoverTab)};
                    }}
                    button:active {{
                        background: {ColorToHex(pThemeColors.ActiveTab)};
                    }}
                "
                
                Dim lProvider As New CssProvider()
                lProvider.LoadFromData(lCss)
                
                ' Apply to all navigation buttons
                ApplyCssToWidget(pLeftScrollButton, lProvider)
                ApplyCssToWidget(pRightScrollButton, lProvider)
                ApplyCssToWidget(pDropdownButton, lProvider)
                ApplyCssToWidget(pHidePanelButton, lProvider)
                
            Catch ex As Exception
                Console.WriteLine($"ApplyButtonStyles error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies CSS provider to a widget
        ''' </summary>
        Private Sub ApplyCssToWidget(vWidget As Widget, vProvider As CssProvider)
            Try
                If vWidget IsNot Nothing Then
                    Dim lContext As StyleContext = vWidget.StyleContext
                    lContext.AddProvider(vProvider, StyleProviderPriority.Application)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyCssToWidget error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses a color string to RGBA
        ''' </summary>
        Private Function ParseColor(vColorString As String) As Gdk.RGBA
            Try
                Dim lColor As New Gdk.RGBA()
                
                If String.IsNullOrEmpty(vColorString) Then
                    Return New Gdk.RGBA() with {.Red = 0, .Green = 0, .Blue = 0, .Alpha = 1}
                End If
                
                ' Remove # if present
                If vColorString.StartsWith("#") Then
                    vColorString = vColorString.Substring(1)
                End If
                
                ' Parse hex color
                If vColorString.Length = 6 Then
                    Dim lR As Integer = Convert.ToInt32(vColorString.Substring(0, 2), 16)
                    Dim lG As Integer = Convert.ToInt32(vColorString.Substring(2, 2), 16)
                    Dim lB As Integer = Convert.ToInt32(vColorString.Substring(4, 2), 16)
                    
                    lColor.Red = lR / 255.0
                    lColor.Green = lG / 255.0
                    lColor.Blue = lB / 255.0
                    lColor.Alpha = 1.0
                End If
                
                Return lColor
                
            Catch ex As Exception
                Console.WriteLine($"ParseColor error: {ex.Message}")
                Return New Gdk.RGBA() with {.Red = 0, .Green = 0, .Blue = 0, .Alpha = 1}
            End Try
        End Function
        
        ''' <summary>
        ''' Converts RGBA to hex string
        ''' </summary>
        Private Function ColorToHex(vColor As Gdk.RGBA) As String
            Try
                Dim lR As Integer = CInt(vColor.Red * 255)
                Dim lG As Integer = CInt(vColor.Green * 255)
                Dim lB As Integer = CInt(vColor.Blue * 255)
                
                Return $"#{lR:X2}{lG:X2}{lB:X2}"
                
            Catch ex As Exception
                Console.WriteLine($"ColorToHex error: {ex.Message}")
                Return "#000000"
            End Try
        End Function
        
        ''' <summary>
        ''' Darkens a color by the specified factor
        ''' </summary>
        Private Function DarkenColor(vColor As Gdk.RGBA, vFactor As Double) As Gdk.RGBA
            Try
                Return New Gdk.RGBA() with {
                    .Red = Math.Max(0, vColor.Red * (1 - vFactor)),
                    .Green = Math.Max(0, vColor.Green * (1 - vFactor)),
                    .Blue = Math.Max(0, vColor.Blue * (1 - vFactor)),
                    .Alpha = vColor.Alpha
                }
                
            Catch ex As Exception
                Console.WriteLine($"DarkenColor error: {ex.Message}")
                Return vColor
            End Try
        End Function
        
        ''' <summary>
        ''' Lightens a color by the specified factor
        ''' </summary>
        Private Function LightenColor(vColor As Gdk.RGBA, vFactor As Double) As Gdk.RGBA
            Try
                Return New Gdk.RGBA() with {
                    .Red = Math.Min(1, vColor.Red + (1 - vColor.Red) * vFactor),
                    .Green = Math.Min(1, vColor.Green + (1 - vColor.Green) * vFactor),
                    .Blue = Math.Min(1, vColor.Blue + (1 - vColor.Blue) * vFactor),
                    .Alpha = vColor.Alpha
                }
                
            Catch ex As Exception
                Console.WriteLine($"LightenColor error: {ex.Message}")
                Return vColor
            End Try
        End Function
        
    End Class
    

End Namespace