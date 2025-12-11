using System.Numerics;

namespace EndingApp;

internal sealed partial class EndingScene
{
    // Helper to calculate total credits height for scrolling
    private float CalculateCreditsHeight()
    {
        if (_fontLoader == null || _credits.Count == 0)
            return 0f;

        int fontSize = _config.Ending.FontSize;
        int sectionFontSize = _config.Ending.SectionFontSize;
        const int valueLineSpacing = 4; // Must match DrawCredits
        const int sectionGap = 8;
        const int sectionSpacing = 40;

        float totalHeight = 0f;
        foreach (var entry in _credits)
        {
            if (entry.IsSeparator)
            {
                totalHeight += fontSize + sectionSpacing;
            }
            else if (entry.Section != null)
            {
                totalHeight += sectionFontSize + sectionGap;
                if (entry.TwoColumns && entry.Values.Count > 0)
                {
                    int total = entry.Values.Count;
                    int rows = (total + 1) / 2;
                    totalHeight += rows * (fontSize + valueLineSpacing);
                }
                else
                {
                    totalHeight += entry.Values.Count * (fontSize + valueLineSpacing);
                }
                totalHeight += sectionSpacing - valueLineSpacing;
            }
        }
        return totalHeight;
    }

    private void DrawCredits()
    {
        if (_fontLoader == null)
            return;

        int fontSize = _config.Ending.FontSize;
        int sectionFontSize = _config.Ending.SectionFontSize;
        const int valueLineSpacing = 4; // Minimal gap between value lines
        const int sectionGap = 8; // Small gap between section title and first value
        const int sectionSpacing = 40; // Larger gap between different sections

        float currentY = _creditsScrollY;
        // Position credits block using creditsPositionPercentage from config
        int creditsLeftX = (int)(
            _config.Ending.Width * _config.Ending.CreditsPositionPercentage / 100.0
        );

        foreach (var entry in _credits)
        {
            if (entry.IsSeparator)
            {
                // Draw separator - treat as a section for uniform spacing
                _fontLoader.DrawText(
                    entry.Separator!,
                    new Vector2(creditsLeftX, currentY),
                    sectionFontSize,
                    _config.Ending.SectionSpacing,
                    _config.Ending.SectionColor,
                    _config.Ending.SectionFontWeight
                );
                currentY += sectionFontSize + sectionSpacing;
            }
            else if (entry.Section != null)
            {
                // Draw section header left-aligned
                _fontLoader.DrawText(
                    entry.Section,
                    new Vector2(creditsLeftX, currentY),
                    sectionFontSize,
                    _config.Ending.SectionSpacing,
                    _config.Ending.SectionColor,
                    _config.Ending.SectionFontWeight
                );
                currentY += sectionFontSize + sectionGap;

                // Draw values with minimal spacing
                if (entry.TwoColumns && entry.Values.Count > 0)
                {
                    // Two column layout: evenly distribute values into two columns
                    int total = entry.Values.Count;
                    int rows = (total + 1) / 2;
                    int col1Count = rows;
                    int col2Count = total - rows;
                    float colGap = 32; // Space between columns
                    float colWidth = 200; // Fixed column width (can be adjusted or measured)
                    for (int row = 0; row < rows; row++)
                    {
                        string left = entry.Values[row];
                        _fontLoader.DrawText(
                            left,
                            new Vector2(creditsLeftX, currentY),
                            fontSize,
                            _config.Ending.ValueSpacing,
                            _config.Ending.ValuesColor,
                            _config.Ending.ValueFontWeight
                        );

                        int rightIdx = row + rows;
                        if (rightIdx < total)
                        {
                            string right = entry.Values[rightIdx];
                            _fontLoader.DrawText(
                                right,
                                new Vector2(creditsLeftX + colWidth + colGap, currentY),
                                fontSize,
                                _config.Ending.ValueSpacing,
                                _config.Ending.ValuesColor,
                                _config.Ending.ValueFontWeight
                            );
                        }

                        currentY += fontSize + valueLineSpacing;
                    }
                }
                else
                {
                    // Single column layout - left-aligned
                    foreach (string value in entry.Values)
                    {
                        _fontLoader.DrawText(
                            value,
                            new Vector2(creditsLeftX, currentY),
                            fontSize,
                            _config.Ending.ValueSpacing,
                            _config.Ending.ValuesColor,
                            _config.Ending.ValueFontWeight
                        );
                        currentY += fontSize + valueLineSpacing;
                    }
                }

                // Add larger gap before next section
                currentY += sectionSpacing - valueLineSpacing;
            }
        }
    }
}
