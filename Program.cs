using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OnlineBookingSystem
{
    class Program
    {
        static void Main(string[] args)
        {
            BookingSystem bookingSystem = new BookingSystem();
            bookingSystem.StartConversation();
        }
    }

    class Service
    {
        public string Name { get; set; } = string.Empty;
        public int Duration { get; set; }
        public decimal Price { get; set; }
    }

    class Booking
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public int Duration { get; set; }

        public override string ToString()
        {
            // Use a safer format for TimeSpan
            string timeStr = $"{Time.Hours}:{Time.Minutes:D2} {(Time.Hours >= 12 ? "PM" : "AM")}";
            return $"Booking #{Id}: {CustomerName} - {ServiceType} on {Date.ToShortDateString()} at {timeStr} ({Duration} min)";
        }
    }

    class BookingSystem
    {
        private List<Service> _services;
        private List<Booking> _bookings = new List<Booking>();
        private int _nextBookingId = 1;
        private readonly HttpClient _httpClient;
        private readonly string _webhookUrl = "https://hook.us2.make.com/fkxyv36ge9onrevwzs6dp4a3w1bafjq3"; // Replace with your actual Make.com webhook URL

        public BookingSystem()
        {
            // Initialize services
            _services = new List<Service>
            {
                new Service { Name = "Haircut", Duration = 30, Price = 30.00m },
                new Service { Name = "Massage", Duration = 60, Price = 60.00m },
                new Service { Name = "Facial", Duration = 45, Price = 45.00m },
                new Service { Name = "Consultation", Duration = 15, Price = 0.00m },
                new Service { Name = "Hair Coloring", Duration = 90, Price = 90.00m }
            };

            _httpClient = new HttpClient();
        }

        public void StartConversation()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("   AI-POWERED BOOKING ASSISTANT");
            Console.WriteLine("==============================================");
            Console.WriteLine("Hi! I'm Grok, your booking assistant.");
            Console.WriteLine("Just tell me what service you want and when.");
            Console.WriteLine("For example: \"I want a haircut tomorrow at 2pm\"");
            Console.WriteLine();

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine() ?? "";
                
                if (input.ToLower() == "exit")
                {
                    Console.WriteLine("Grok: Thanks for using our service. Goodbye!");
                    break;
                }

                // Try to extract booking information
                var (service, date, time) = ExtractBookingInfo(input);

                if (service != null && date.HasValue && time.HasValue)
                {
                    // Try to find the service
                    var selectedService = _services.FirstOrDefault(s => 
                        s.Name.Equals(service, StringComparison.OrdinalIgnoreCase));

                    if (selectedService == null)
                    {
                        Console.WriteLine($"Grok: Sorry, we don't offer '{service}'. Our services are: Haircut, Massage, Facial, Consultation, and Hair Coloring.");
                        continue;
                    }

                    // Get the customer name
                    Console.Write("Grok: Great! What's your name? ");
                    string customerName = Console.ReadLine() ?? "Guest";

                    // Create the booking
                    Booking booking = new Booking
                    {
                        Id = _nextBookingId++,
                        CustomerName = customerName,
                        ServiceType = selectedService.Name,
                        Date = date.Value,
                        Time = time.Value,
                        Duration = selectedService.Duration
                    };

                    _bookings.Add(booking);
                    
                    // Use a safer way to format the time
                    string timeStr = $"{booking.Time.Hours}:{booking.Time.Minutes:D2} {(booking.Time.Hours >= 12 ? "PM" : "AM")}";
                    Console.WriteLine($"Grok: Perfect! I've booked your {booking.ServiceType} for {booking.Date.ToShortDateString()} at {timeStr}.");
                    
                    // Send booking data to Make.com webhook
                    try
                    {
                        var webhookSent = SendBookingToWebhook(booking).GetAwaiter().GetResult();
                        if (webhookSent)
                        {
                            Console.WriteLine($"Grok: Your booking ID is #{booking.Id} and has been confirmed in our system.");
                        }
                        else
                        {
                            Console.WriteLine($"Grok: Your booking ID is #{booking.Id}. Note: There was an issue syncing with our calendar system, but your booking is still valid.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Grok: Your booking ID is #{booking.Id}. Note: Could not sync with external systems: {ex.Message}");
                    }
                    
                    Console.WriteLine($"Grok: Is there anything else I can help with?");
                }
                else
                {
                    if (service == null)
                    {
                        Console.WriteLine("Grok: I didn't catch what service you wanted. We offer Haircut, Massage, Facial, Consultation, and Hair Coloring.");
                    }
                    else if (!date.HasValue)
                    {
                        Console.WriteLine("Grok: I need to know what day you want to book. For example, 'tomorrow' or 'Monday' or a specific date.");
                    }
                    else if (!time.HasValue)
                    {
                        Console.WriteLine("Grok: I need to know what time you want to book. For example, '2pm' or '3:30'.");
                    }
                }
            }
        }

        private async Task<bool> SendBookingToWebhook(Booking booking)
        {
            try
            {
                // Format date and time for Make.com
                string formattedDate = booking.Date.ToString("yyyy-MM-dd");
                string formattedTime = $"{booking.Time.Hours}:{booking.Time.Minutes:D2}";
                
                // Create data payload to send to Make.com
                var bookingData = new
                {
                    id = booking.Id,
                    customerName = booking.CustomerName,
                    serviceType = booking.ServiceType,
                    date = formattedDate,
                    time = formattedTime,
                    duration = booking.Duration,
                    timestamp = DateTime.Now.ToString("o") // ISO 8601 format with timezone
                };

                // Convert to JSON
                string jsonContent = JsonSerializer.Serialize(bookingData);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                // Send to webhook
                Console.WriteLine("Grok: Confirming your booking with our system...");
                var response = await _httpClient.PostAsync(_webhookUrl, content);
                
                // Check response
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    Console.WriteLine($"Webhook error: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to webhook: {ex.Message}");
                return false;
            }
        }

     private (string? Service, DateTime? Date, TimeSpan? Time) ExtractBookingInfo(string input)
    {
        input = input.ToLower();
        
        // Extract service (same as before)
        string? service = null;
        string[] serviceKeywords = { "haircut", "massage", "facial", "consultation", "hair coloring" };
        
        foreach (var keyword in serviceKeywords)
        {
            if (input.Contains(keyword))
            {
                service = char.ToUpper(keyword[0]) + keyword.Substring(1);
                break;
            }
        }

        // Extract date - enhanced version
        DateTime? date = null;
        
        // Check for relative dates
        if (input.Contains("tomorrow"))
        {
            date = DateTime.Today.AddDays(1);
        }
        else if (input.Contains("today"))
        {
            date = DateTime.Today;
        }
        else if (input.Contains("day after tomorrow"))
        {
            date = DateTime.Today.AddDays(2);
        }
        else if (Regex.IsMatch(input, @"next\s+week"))
        {
            date = DateTime.Today.AddDays(7);
        }
        else
        {
            // Try to extract month dates like "March 15" or "15th of March"
            var monthNameRegex = new Regex(@"(january|february|march|april|may|june|july|august|september|october|november|december)\s+(\d{1,2})(?:st|nd|rd|th)?");
            var monthNameMatch = monthNameRegex.Match(input);
            
            var dayMonthRegex = new Regex(@"(\d{1,2})(?:st|nd|rd|th)?\s+(?:of\s+)?(january|february|march|april|may|june|july|august|september|october|november|december)");
            var dayMonthMatch = dayMonthRegex.Match(input);
            
            if (monthNameMatch.Success)
            {
                string monthName = monthNameMatch.Groups[1].Value;
                int day = int.Parse(monthNameMatch.Groups[2].Value);
                int month = GetMonthNumber(monthName);
                
                if (month > 0 && day > 0 && day <= DateTime.DaysInMonth(DateTime.Now.Year, month))
                {
                    date = new DateTime(DateTime.Now.Year, month, day);
                    
                    // If the date is in the past, assume next year
                    if (date < DateTime.Today)
                    {
                        date = date.Value.AddYears(1);
                    }
                }
            }
            else if (dayMonthMatch.Success)
            {
                int day = int.Parse(dayMonthMatch.Groups[1].Value);
                string monthName = dayMonthMatch.Groups[2].Value;
                int month = GetMonthNumber(monthName);
                
                if (month > 0 && day > 0 && day <= DateTime.DaysInMonth(DateTime.Now.Year, month))
                {
                    date = new DateTime(DateTime.Now.Year, month, day);
                    
                    // If the date is in the past, assume next year
                    if (date < DateTime.Today)
                    {
                        date = date.Value.AddYears(1);
                    }
                }
            }
            else
            {
                // Try to parse various date formats: MM/DD, MM/DD/YYYY, DD/MM, DD/MM/YYYY
                                var dateRegex = new Regex(@"\b(\d{1,2})[/.-](\d{1,2})(?:[/.-](\d{2,4}))?\b");
                    
                    // Handle special case for "next week Wednesday" type expressions
                    var nextWeekDayRegex = new Regex(@"next\s+week\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)", RegexOptions.IgnoreCase);
                var dateMatch = dateRegex.Match(input);
                
                if (dateMatch.Success)
                {
                    int firstNum = int.Parse(dateMatch.Groups[1].Value);
                    int secondNum = int.Parse(dateMatch.Groups[2].Value);
                    int year = dateMatch.Groups[3].Success ? int.Parse(dateMatch.Groups[3].Value) : DateTime.Now.Year;
                    
                    // Adjust 2-digit year
                    if (year < 100)
                    {
                        year += 2000;
                    }
                    
                    // Try MM/DD first (US format)
                    if (firstNum <= 12 && secondNum <= 31)
                    {
                        try
                        {
                            date = new DateTime(year, firstNum, secondNum);
                        }
                        catch
                        {
                            // If MM/DD fails, try DD/MM (European format)
                            if (secondNum <= 12 && firstNum <= 31)
                            {
                                try
                                {
                                    date = new DateTime(year, secondNum, firstNum);
                                }
                                catch
                                {
                                    // Invalid date
                                }
                            }
                        }
                    }
                    // Try DD/MM (European format)
                    else if (secondNum <= 12 && firstNum <= 31)
                    {
                        try
                        {
                            date = new DateTime(year, secondNum, firstNum);
                        }
                        catch
                        {
                            // Invalid date
                        }
                    }
                }
                
                // Check for day names and more complex date expressions
                if (date == null)
                {
                    string[] dayNames = { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };
                    for (int i = 0; i < dayNames.Length; i++)
                    {
                        if (input.Contains(dayNames[i]))
                        {
                            DayOfWeek targetDay = (DayOfWeek)i;
                            
                            // Check for patterns like "next week wednesday"
                            var nextWeekDayMatch = nextWeekDayRegex.Match(input);
                            if (nextWeekDayMatch.Success)
                            {
                                date = GetNextDayOfWeek(targetDay, skipCurrentWeek: true);
                                break;
                            }
                            
                            // Check for "next [day]" or "next week [day]"
                            if (Regex.IsMatch(input, $@"next\s+(?:week\s+)?{dayNames[i]}"))
                            {
                                date = GetNextDayOfWeek(targetDay, skipCurrentWeek: true);
                            }
                            else
                            {
                                date = GetNextDayOfWeek(targetDay);
                            }
                            break;
                        }
                    }
                }
                
                // Check for "in X days"
                var inDaysMatch = Regex.Match(input, @"in\s+(\d+)\s+days?");
                if (inDaysMatch.Success)
                {
                    int daysToAdd = int.Parse(inDaysMatch.Groups[1].Value);
                    date = DateTime.Today.AddDays(daysToAdd);
                }
            }
        }

        // Extract time - enhanced version
        TimeSpan? time = null;
        
        // Look for patterns like "at 2pm", "at 2:30 pm", "2 pm", "2:30pm", "14:30", "14h30", "2:30"
        var timeRegexes = new List<Regex>
        {
            new Regex(@"(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)", RegexOptions.IgnoreCase),   // 2pm, 2:30pm
            new Regex(@"(?:at\s+)?(\d{1,2})(?::|h)(\d{2})\b"),                                    // 14:30, 14h30
            new Regex(@"(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(?:o'clock)?\b")                       // 2, 2:30, 2 o'clock
        };
        
        foreach (var regex in timeRegexes)
        {
            var match = regex.Match(input);
            if (match.Success)
            {
                int hour = int.Parse(match.Groups[1].Value);
                int minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                
                // Check for AM/PM
                if (match.Groups.Count > 3 && match.Groups[3].Success)
                {
                    string ampm = match.Groups[3].Value.ToLower();
                    if (ampm == "pm" && hour < 12)
                    {
                        hour += 12;
                    }
                    else if (ampm == "am" && hour == 12)
                    {
                        hour = 0;
                    }
                }
                // For 24-hour format or when no AM/PM is specified
                else
                {
                    // If hour is less than 12 and no AM/PM specified, we need to check context
                    if (hour < 12 && !match.Groups[3].Success)
                    {
                        // Try to determine from context if it's morning or evening
                        if (input.Contains("evening") || input.Contains("night"))
                        {
                            hour += 12;
                        }
                        else if (input.Contains("afternoon") && hour < 6)
                        {
                            hour += 12;
                        }
                    }
                }
                
                // Validate the time
                if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                {
                    time = new TimeSpan(hour, minute, 0);
                    break;
                }
            }
        }
        
        // Also check for specific times of day
        if (time == null)
        {
            if (input.Contains("morning"))
            {
                time = new TimeSpan(9, 0, 0);  // 9:00 AM default for morning
            }
            else if (input.Contains("afternoon"))
            {
                time = new TimeSpan(14, 0, 0); // 2:00 PM default for afternoon
            }
            else if (input.Contains("evening"))
            {
                time = new TimeSpan(18, 0, 0); // 6:00 PM default for evening
            }
            else if (input.Contains("night"))
            {
                time = new TimeSpan(19, 0, 0); // 7:00 PM default for night
            }
        }

        return (service, date, time);
    }

    private int GetMonthNumber(string monthName)
    {
        switch (monthName.ToLower())
        {
            case "january": return 1;
            case "february": return 2;
            case "march": return 3;
            case "april": return 4;
            case "may": return 5;
            case "june": return 6;
            case "july": return 7;
            case "august": return 8;
            case "september": return 9;
            case "october": return 10;
            case "november": return 11;
            case "december": return 12;
            default: return 0;
        }
    }

    private DateTime GetNextDayOfWeek(DayOfWeek dayOfWeek, bool skipCurrentWeek = false)
    {
        DateTime result = DateTime.Today;
        
        // Calculate days to add to get to the desired day of week
        int daysToAdd = ((int)dayOfWeek - (int)result.DayOfWeek + 7) % 7;
        
        // If today is the target day
        if (daysToAdd == 0)
        {
            if (skipCurrentWeek)
            {
                // Skip to next week
                daysToAdd = 7;
            }
            // Otherwise keep it as today
        }
        // If the target day is in the future this week but we want to skip to next week
        else if (skipCurrentWeek)
        {
            daysToAdd += 7;
        }
        
        return result.AddDays(daysToAdd);
     }
    }
}