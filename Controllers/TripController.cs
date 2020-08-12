using System;
using System.IO;
using System.Text.RegularExpressions;


using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


namespace TripAPI.Controllers
{
    public class TripDTO
    {
        public string name { get; set; }
        public int miles { get; set; }
        public int mph { get; set; }
        public TripDTO()
        {
        }
    }

    public class Time
    {

        public static bool isTime(string time)
        {
            Regex timeRegex = new Regex("^(([01]?[0-9]|2[0-3]):[0-5][0-9])$");
            return timeRegex.IsMatch(time);
        }

        public int hour;
        public int minute;
        public Time(string time)
        {
            string[] timeSplit = time.Split(":");
            this.hour = Int16.Parse(timeSplit[0]);
            this.minute = Int16.Parse(timeSplit[1]);
        }

        public Time(int hour, int minute)
        {
            this.hour = hour;
            this.minute = minute;
        }

        public int compare(Time compareTime)
        {
            //Returns 0 if time is equal to compareTime
            if (this.hour == compareTime.hour && this.minute == compareTime.minute)
            {
                return 0;
            }
            //Return -1 if time is less than compareTime
            if ((this.hour < compareTime.hour) || (this.hour == compareTime.hour && this.minute < compareTime.minute))
            {
                return -1;
            }
            //Return 1 if time is greater than compareTime
            return 1;
        }


        public Time difference(Time compareTime)
        {
            if (this.compare(compareTime) < 0)
            {
                throw new System.ArgumentException("Time cannot be less than compareTime");
            }
            int hourDifference = 0;
            int minuteDifference = 0;
            minuteDifference = this.minute - compareTime.minute;
            if (minuteDifference < 0)
            {
                hourDifference = -1;
                minuteDifference = minuteDifference + 60;
            }
            hourDifference = hourDifference + (this.hour - compareTime.hour);

            return new Time(hourDifference, minuteDifference);
        }

        public double toDouble()
        {
            return this.hour + ((double)this.minute / 60);
        }


    }

    public class Trip
    {
        public Time startTime;
        public Time endTime;
        public double milesDriven;
        public double tripDuration
        {
            get => this.endTime.difference(startTime).toDouble();
        }

        public double milesPerHour
        {
            get => this.milesDriven / this.tripDuration;
        }

        public Trip(Time startTime, Time endTime, double milesDriven)
        {
            this.startTime = startTime;
            this.endTime = endTime;
            this.milesDriven = milesDriven;
        }

    }

    public class Driver
    {
        public string name;
        public List<Trip> trips = new List<Trip>();
        public double totalMiles;
        public double tripsMilesPerHour;

        public Driver(string name)
        {
            this.name = name;
        }

        public void addTrip(Trip trip)
        {
            this.trips.Add(trip);
        }

        public void calculateTrips()
        {
            double totalMiles = 0;
            double totalDuration = 0;
            foreach (Trip trip in this.trips)
            {
                double milesPerHour = trip.milesPerHour;
                if (milesPerHour >= 5 && milesPerHour <= 100)
                {
                    totalMiles = totalMiles + trip.milesDriven;
                    totalDuration = totalDuration + trip.tripDuration;
                }
            }
            this.totalMiles = totalMiles;
            this.tripsMilesPerHour = this.totalMiles / totalDuration;
        }

    }

    public abstract class Compiler
    {

        public static void executeDriverCommand(int lineNumber, string[] currentLineSplit, List<Driver> drivers)
        {
            if (currentLineSplit.Length != 2)
            {
                invalidNumberOfArguments(lineNumber, "Driver");
            }
            drivers.Add(new Driver(currentLineSplit[1]));
        }


        public static void invalidNumberOfArguments(int lineNumber, string command)
        {
            int numOfArgs = command == "Driver" ? 1 : 4;
            badRequest(lineNumber, "Invalid number of arguments for " + command + ". Expected " + numOfArgs + " arguments.");
        }

        public static IActionResult badRequest(int lineNumber, string message)
        {
            throw new Exception("Line " + lineNumber + ": " + message);
        }

        public static void executeTripCommand(int lineNumber, string[] currentLineSplit, List<Driver> drivers)
        {
            if (currentLineSplit.Length != 5)
            {
                invalidNumberOfArguments(lineNumber, "Trip");
            }


            string tripDriver = currentLineSplit[1];
            Driver driver = drivers.Find(driver => driver.name == tripDriver);
            if (driver == null)
            {
                badRequest(lineNumber, "Driver " + tripDriver + " has not been instantiated.");
            }

            string startTime = currentLineSplit[2];
            string endTime = currentLineSplit[3];
            string tripDistanceString = currentLineSplit[4];
            double tripDistance = 0;
            if (!Time.isTime(startTime))
            {
                badRequest(lineNumber, "Start time is not in valid format.");
            }
            if (!Time.isTime(endTime))
            {
                badRequest(lineNumber, "End time is not in valid format.");
            }
            try
            {
                tripDistance = double.Parse(tripDistanceString);
            }
            catch
            {
                badRequest(lineNumber, "Trip distance is not in valid format.");

            }

            Time start = new Time(startTime);
            Time end = new Time(endTime);
            Trip currentTrip = new Trip(start, end, tripDistance);
            driver.addTrip(currentTrip);

        }

    }


    [ApiController]
    [Route("[controller]")]
    public class TripController : ControllerBase
    {


        public TripController()
        {
        }

        [HttpPost("uploadAndCompile")]
        public async Task<IActionResult> compileFile()
        {
            try
            {
                var file = Request.Form.Files[0];
                if (file.ContentType != "text/plain")
                {
                    return BadRequest("File must be a text file");
                }

                List<Driver> drivers = new List<Driver>();

                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    int lineNumber = 0;
                    int tripCount = 0;
                    while (reader.Peek() >= 0)
                    {
                        lineNumber++;
                        var currentLine = await reader.ReadLineAsync();

                        if (currentLine != "")
                        {
                            string[] currentLineSplit = currentLine.Split(' ');
                            string currentCommand = currentLineSplit[0];
                            if (currentCommand == "Driver")
                            {
                                Compiler.executeDriverCommand(lineNumber, currentLineSplit, drivers);
                            }
                            else if (currentCommand == "Trip")
                            {
                                tripCount++;
                                Compiler.executeTripCommand(lineNumber, currentLineSplit, drivers);
                            }
                            else
                            {
                                Compiler.badRequest(lineNumber, "Command " + currentCommand + " is invalid.");
                            }

                        }
                    }

                    if (tripCount == 0)
                    {
                        Compiler.badRequest(0, "No trip command has been called");
                    }
                }

                List<TripDTO> result = new List<TripDTO>();

                foreach (Driver driver in drivers)
                {
                    driver.calculateTrips();
                    int totalMiles = (int)Math.Round(driver.totalMiles);
                    int tripsMilesPerHour = driver.totalMiles == 0 ? 0 : (int)Math.Round(driver.tripsMilesPerHour);
                    TripDTO driverTrip = new TripDTO()
                    {
                        name = driver.name,
                        miles = totalMiles,
                        mph = tripsMilesPerHour,
                    };
                    result.Add(driverTrip);

                }

                return new OkObjectResult(result);


            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }


        }


    }
}
