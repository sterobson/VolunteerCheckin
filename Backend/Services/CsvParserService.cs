namespace VolunteerCheckin.Functions.Services;

public class CsvParserService
{
    public class LocationCsvRow
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasLatLong { get; set; }
        public List<string> MarshalNames { get; set; } = [];
        public string What3Words { get; set; } = string.Empty;
    }

    public class CsvParseResult
    {
        public List<LocationCsvRow> Locations { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }

    public class MarshalCsvRow
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Checkpoint { get; set; } = string.Empty;
    }

    public class MarshalCsvParseResult
    {
        public List<MarshalCsvRow> Marshals { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }

    public static CsvParseResult ParseLocationsCsv(Stream fileStream)
    {
        CsvParseResult result = new();

        try
        {
            using StreamReader reader = new(fileStream);
            // Read header row
            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                result.Errors.Add("CSV file is empty");
                return result;
            }

            string[] headers = [.. headerLine.Split(',').Select(h => h.Trim().Trim('"'))];

            // Find column indices (case-insensitive)
            int labelIndex = FindColumnIndex(headers, "Label", "Name", "Checkpoint", "Position");
            int descriptionIndex = FindColumnIndex(headers, "Description", "Detail", "Details", "");
            int latIndex = FindColumnIndex(headers, "Lat", "Latitude", "Lat (Optional)", "Latitude (Optional)");
            int lonIndex = FindColumnIndex(headers, "Long", "Longitude", "Lon", "Long (Optional)", "Longitude (Optional)", "Lon (Optional)");
            int marshalsIndex = FindColumnIndex(headers, "Marshal*", "People*");
            int what3WordsIndex = FindColumnIndex(headers, "What3Words*", "W3W*");

            // Validate required columns
            if (labelIndex == -1)
            {
                result.Errors.Add("Missing required column: Label");
                return result;
            }

            // Lat/Long are optional for updates

            // Parse data rows
            int rowNumber = 1;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line.Replace(",", ""))) continue;

                try
                {
                    string[] values = ParseCsvLine(line);

                    if (values.Length <= Math.Max(labelIndex, Math.Max(latIndex, lonIndex)))
                    {
                        result.Errors.Add($"Row {rowNumber}: Insufficient columns");
                        continue;
                    }

                    string name = values[labelIndex].Trim().Trim('"');

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        result.Errors.Add($"Row {rowNumber}: Label is empty");
                        continue;
                    }

                    // Parse lat/long if columns exist and have values
                    double lat = 0;
                    double lon = 0;
                    bool hasLatLong = false;

                    if (latIndex != -1 && values.Length > latIndex)
                    {
                        string latStr = values[latIndex].Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(latStr))
                        {
                            if (double.TryParse(latStr, out lat) && lat >= -90 && lat <= 90)
                            {
                                hasLatLong = true;
                            }
                            else
                            {
                                result.Errors.Add($"Row {rowNumber}: Invalid latitude value '{latStr}'");
                                continue;
                            }
                        }
                    }

                    if (lonIndex != -1 && values.Length > lonIndex)
                    {
                        string lonStr = values[lonIndex].Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(lonStr))
                        {
                            if (double.TryParse(lonStr, out lon) && lon >= -180 && lon <= 180)
                            {
                                hasLatLong = hasLatLong && true;
                            }
                            else
                            {
                                result.Errors.Add($"Row {rowNumber}: Invalid longitude value '{lonStr}'");
                                continue;
                            }
                        }
                    }

                    string details = string.Empty;
                    if (descriptionIndex >= 0 && descriptionIndex < values.Length)
                    {
                        details = values[descriptionIndex];
                    }

                    LocationCsvRow location = new()
                    {
                        Name = name,
                        Description = details,
                        Latitude = lat,
                        Longitude = lon,
                        HasLatLong = hasLatLong
                    };

                    // Parse marshals if column exists
                    if (marshalsIndex != -1 && values.Length > marshalsIndex)
                    {
                        string marshalsStr = values[marshalsIndex].Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(marshalsStr))
                        {
                            location.MarshalNames = [.. marshalsStr
                                    .Split([",", "&", " and ", "+"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries )
                                    .Select(m => m.Trim())
                                    .Where(m => !string.IsNullOrWhiteSpace(m))];

                            // If anyone is missing a surname, but they appeared with an "&" and a subsequent person had a surname, then use that surname.
                            // E.g. "John & Jane Smith" -> "John Smith" and "Jane Smith"
                            // E.g. "John and Jane Smith" -> "John Smith" and "Jane Smith"
                            // E.g. "John and Jane and Joan Smith" -> "John Smith" and "Jane Smith" and Joan Smith
                            // E.g. "John, Jane, & Joan Smith" -> "John Smith" and "Jane Smith" and Joan Smith

                            int i = 0;
                            while (i < location.MarshalNames.Count)
                            {
                                string marshalName = location.MarshalNames[i];

                                string[] nameParts = marshalName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                if (nameParts.Length == 1)
                                {
                                    if (i > 0 && int.TryParse(marshalName, out int number))
                                    {
                                        // It's a number, so would be like "John Smith, +1",
                                        // or "John Smith +1". We want the marshal name to become "John Smith (+1)", "John Smith (+2)", etc.
                                        location.MarshalNames = [
                                            .. location.MarshalNames.Take(i),
                                            ..Enumerable.Range(1, number).Select(n => MakeNamePossessive(location.MarshalNames[i - 1]) + " (+" + n + ")"),
                                            .. location.MarshalNames.Skip(i+1)
                                        ];

                                        i += number; // Skip the newly added entries
                                        continue;
                                    }

                                    // Missing surname, look ahead for a surname
                                    bool foundSurname = false;
                                    for (int j = i + 1; j < location.MarshalNames.Count; j++)
                                    {
                                        string nextMarshalName = location.MarshalNames[j];
                                        string[] nextNameParts = nextMarshalName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                        if (nextNameParts.Length > 1)
                                        {
                                            // Found a surname to use
                                            string surname = nextNameParts[^1];
                                            location.MarshalNames[i] = marshalName + " " + surname;
                                            foundSurname = true;
                                            break;
                                        }
                                    }
                                    if (foundSurname)
                                    {
                                        i++;
                                        continue;
                                    }
                                }

                                i++;
                            }
                        }
                    }

                    // Parse What3Words if column exists
                    if (what3WordsIndex != -1 && values.Length > what3WordsIndex)
                    {
                        string what3WordsStr = values[what3WordsIndex].Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(what3WordsStr))
                        {
                            location.What3Words = what3WordsStr;
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
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse CSV file: {ex.Message}");
        }

        return result;
    }

    private static string MakeNamePossessive(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        name = name.Trim();
        if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            return name + "'";
        }
        else
        {
            return name + "'s";
        }
    }

    private static int FindColumnIndex(string[] headers, params string[] possibleNames)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            foreach (string name in possibleNames)
            {
                string trimmedHeader = headers[i].Replace(" ", "");
                string trimmedName = name.Replace(" ", "");
                if (trimmedHeader.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
                else if (trimmedName.EndsWith('*') && trimmedHeader.StartsWith(trimmedName.Replace("*", ""), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    public static MarshalCsvParseResult ParseMarshalsCsv(Stream fileStream)
    {
        MarshalCsvParseResult result = new();

        try
        {
            using StreamReader reader = new(fileStream);

            // Read header row
            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                result.Errors.Add("CSV file is empty");
                return result;
            }

            string[] headers = [.. headerLine.Split(',').Select(h => h.Trim().Trim('"'))];

            // Find column indices (case-insensitive, handle "(optional)" suffix)
            int nameIndex = FindColumnIndex(headers, "Name");
            int emailIndex = FindColumnIndex(headers, "Email", "Email (optional)");
            int phoneIndex = FindColumnIndex(headers, "Phone", "Phone (optional)", "Phone Number", "Phone Number (optional)");
            int checkpointIndex = FindColumnIndex(headers, "Checkpoint", "Checkpoint (optional)", "Location", "Location (optional)");

            // Validate required columns
            if (nameIndex == -1)
            {
                result.Errors.Add("Missing required column: Name");
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

                    if (values.Length <= nameIndex)
                    {
                        result.Errors.Add($"Row {rowNumber}: Insufficient columns");
                        continue;
                    }

                    string name = values[nameIndex].Trim().Trim('"');

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        result.Errors.Add($"Row {rowNumber}: Name is empty");
                        continue;
                    }

                    string email = emailIndex != -1 && values.Length > emailIndex
                        ? values[emailIndex].Trim().Trim('"')
                        : string.Empty;

                    string phone = phoneIndex != -1 && values.Length > phoneIndex
                        ? values[phoneIndex].Trim().Trim('"')
                        : string.Empty;

                    string checkpoint = checkpointIndex != -1 && values.Length > checkpointIndex
                        ? values[checkpointIndex].Trim().Trim('"')
                        : string.Empty;

                    result.Marshals.Add(new MarshalCsvRow
                    {
                        Name = name,
                        Email = email,
                        Phone = phone,
                        Checkpoint = checkpoint
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse CSV file: {ex.Message}");
        }

        return result;
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> values = [];
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
        return [.. values];
    }
}
