using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Services;

public class CsvParserService
{
    public class LocationCsvRow
    {
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<string> MarshalNames { get; set; } = new List<string>();
    }

    public class CsvParseResult
    {
        public List<LocationCsvRow> Locations { get; set; } = new List<LocationCsvRow>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public CsvParseResult ParseLocationsCsv(Stream fileStream)
    {
        CsvParseResult result = new CsvParseResult();

        try
        {
            using (StreamReader reader = new StreamReader(fileStream))
            {
                // Read header row
                string? headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    result.Errors.Add("CSV file is empty");
                    return result;
                }

                string[] headers = headerLine.Split(',').Select(h => h.Trim().Trim('"')).ToArray();

                // Find column indices (case-insensitive)
                int labelIndex = FindColumnIndex(headers, "Label");
                int latIndex = FindColumnIndex(headers, "Lat", "Latitude");
                int lonIndex = FindColumnIndex(headers, "Long", "Longitude", "Lon");
                int marshalsIndex = FindColumnIndex(headers, "Marshals", "Marshal");

                // Validate required columns
                if (labelIndex == -1)
                {
                    result.Errors.Add("Missing required column: Label");
                    return result;
                }
                if (latIndex == -1)
                {
                    result.Errors.Add("Missing required column: Lat or Latitude");
                    return result;
                }
                if (lonIndex == -1)
                {
                    result.Errors.Add("Missing required column: Long, Longitude, or Lon");
                    return result;
                }

                // Parse data rows
                int rowNumber = 1;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    rowNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        string[] values = ParseCsvLine(line);

                        if (values.Length <= Math.Max(labelIndex, Math.Max(latIndex, lonIndex)))
                        {
                            result.Errors.Add($"Row {rowNumber}: Insufficient columns");
                            continue;
                        }

                        string name = values[labelIndex].Trim().Trim('"');
                        string latStr = values[latIndex].Trim().Trim('"');
                        string lonStr = values[lonIndex].Trim().Trim('"');

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            result.Errors.Add($"Row {rowNumber}: Label is empty");
                            continue;
                        }

                        if (!double.TryParse(latStr, out double lat) || lat < -90 || lat > 90)
                        {
                            result.Errors.Add($"Row {rowNumber}: Invalid latitude value '{latStr}'");
                            continue;
                        }

                        if (!double.TryParse(lonStr, out double lon) || lon < -180 || lon > 180)
                        {
                            result.Errors.Add($"Row {rowNumber}: Invalid longitude value '{lonStr}'");
                            continue;
                        }

                        LocationCsvRow location = new LocationCsvRow
                        {
                            Name = name,
                            Latitude = lat,
                            Longitude = lon
                        };

                        // Parse marshals if column exists
                        if (marshalsIndex != -1 && values.Length > marshalsIndex)
                        {
                            string marshalsStr = values[marshalsIndex].Trim().Trim('"');
                            if (!string.IsNullOrWhiteSpace(marshalsStr))
                            {
                                location.MarshalNames = marshalsStr
                                    .Split(',')
                                    .Select(m => m.Trim())
                                    .Where(m => !string.IsNullOrWhiteSpace(m))
                                    .ToList();
                            }
                        }

                        result.Locations.Add(location);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse CSV file: {ex.Message}");
        }

        return result;
    }

    private int FindColumnIndex(string[] headers, params string[] possibleNames)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            foreach (string name in possibleNames)
            {
                if (headers[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private string[] ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        bool inQuotes = false;
        string currentValue = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(currentValue);
                currentValue = "";
            }
            else
            {
                currentValue += c;
            }
        }

        values.Add(currentValue);
        return values.ToArray();
    }
}
