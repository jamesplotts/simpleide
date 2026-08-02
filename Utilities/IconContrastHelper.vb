' Utilities/IconContrastHelper.vb - Shared icon contrast-inversion logic used by any
' custom-drawn widget that paints a Gdk.Pixbuf icon and needs it to stay visible against
' whichever theme background it currently sits on
Imports System.Runtime.InteropServices

Namespace Utilities

    ''' <summary>
    ''' Samples a pixbuf's predominant luminance and, when it would blend into a same-toned
    ''' background, produces a color-inverted copy - originally written for CustomDrawButton
    ''' (icon-only nav buttons, toolbar buttons), extracted here so CustomDrawNotebook's tab
    ''' icons can use the exact same, already-proven logic instead of a second copy of it
    ''' </summary>
    Public Module IconContrastHelper

        ''' <summary>
        ''' Samples a pixbuf's opaque pixels and reports whether its average luminance is
        ''' predominantly dark
        ''' </summary>
        ''' <param name="vPixbuf">Icon to sample - Nothing/fully-transparent returns False</param>
        ''' <returns>True if the icon's average luminance is dark (below the midpoint)</returns>
        Public Function ComputeIsDark(vPixbuf As Gdk.Pixbuf) As Boolean
            Try
                If vPixbuf Is Nothing Then Return False

                Dim lChannels As Integer = vPixbuf.NChannels
                Dim lHasAlpha As Boolean = vPixbuf.HasAlpha
                Dim lRowstride As Integer = vPixbuf.Rowstride
                Dim lWidth As Integer = vPixbuf.Width
                Dim lHeight As Integer = vPixbuf.Height
                Dim lLength As Integer = lRowstride * lHeight
                If lLength <= 0 Then Return False

                Dim lBytes(lLength - 1) As Byte
                Marshal.Copy(vPixbuf.Pixels, lBytes, 0, lLength)

                Dim lLuminanceSum As Double = 0
                Dim lOpaquePixelCount As Integer = 0

                for lY As Integer = 0 To lHeight - 1
                    Dim lRowStart As Integer = lY * lRowstride
                    for lX As Integer = 0 To lWidth - 1
                        Dim lOffset As Integer = lRowStart + lX * lChannels
                        If lHasAlpha AndAlso lBytes(lOffset + 3) < 32 Then Continue for ' skip near-transparent pixels
                        Dim lR As Byte = lBytes(lOffset)
                        Dim lG As Byte = lBytes(lOffset + 1)
                        Dim lB As Byte = lBytes(lOffset + 2)
                        lLuminanceSum += (0.299 * lR + 0.587 * lG + 0.114 * lB)
                        lOpaquePixelCount += 1
                    Next
                Next

                If lOpaquePixelCount = 0 Then Return False ' fully transparent icon - nothing to judge

                Dim lAverageLuminance As Double = lLuminanceSum / lOpaquePixelCount ' 0 (black) .. 255 (white)
                Return lAverageLuminance < 128.0

            Catch ex As Exception
                Console.WriteLine($"IconContrastHelper.ComputeIsDark error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Returns a copy of a pixbuf with its RGB channels inverted (255-value), leaving
        ''' alpha untouched - turns dark line-art into light line-art (and vice versa) while
        ''' keeping transparent background pixels transparent
        ''' </summary>
        Public Function Invert(vPixbuf As Gdk.Pixbuf) As Gdk.Pixbuf
            Try
                Dim lCopy As Gdk.Pixbuf = vPixbuf.Copy()
                Dim lChannels As Integer = lCopy.NChannels
                Dim lHasAlpha As Boolean = lCopy.HasAlpha
                Dim lRowstride As Integer = lCopy.Rowstride
                Dim lWidth As Integer = lCopy.Width
                Dim lHeight As Integer = lCopy.Height
                Dim lLength As Integer = lRowstride * lHeight
                If lLength <= 0 Then Return lCopy

                Dim lBytes(lLength - 1) As Byte
                Marshal.Copy(lCopy.Pixels, lBytes, 0, lLength)

                for lY As Integer = 0 To lHeight - 1
                    Dim lRowStart As Integer = lY * lRowstride
                    for lX As Integer = 0 To lWidth - 1
                        Dim lOffset As Integer = lRowStart + lX * lChannels
                        If lHasAlpha AndAlso lBytes(lOffset + 3) = 0 Then Continue for ' nothing visible to invert
                        lBytes(lOffset) = CByte(255 - lBytes(lOffset))
                        lBytes(lOffset + 1) = CByte(255 - lBytes(lOffset + 1))
                        lBytes(lOffset + 2) = CByte(255 - lBytes(lOffset + 2))
                    Next
                Next

                Marshal.Copy(lBytes, 0, lCopy.Pixels, lLength)
                Return lCopy

            Catch ex As Exception
                Console.WriteLine($"IconContrastHelper.Invert error: {ex.Message}")
                Return vPixbuf
            End Try
        End Function

    End Module

End Namespace
