using Cwiczenie88.Models.DTOS;
using Microsoft.AspNetCore.Mvc;

using Microsoft.Data.SqlClient;

namespace TravelAgencyAPI.Controllers
{
    public class TripsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public TripsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult GetTrips()
        {
            var trips = new List<object>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(@"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                       c.Name
                FROM Trip t
                JOIN Country_Trip ct ON ct.IdTrip = t.IdTrip
                JOIN Country c ON c.IdCountry = ct.IdCountry
            ", connection)) // otrzymanie dannych o trip
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        trips.Add(new
                        {
                            IdTrip = reader["IdTrip"],
                            Name = reader["Name"],
                            Description = reader["Description"],
                            DateFrom = reader["DateFrom"],
                            DateTo = reader["DateTo"],
                            MaxPeople = reader["MaxPeople"],
                            Country = reader["Country"]
                        });
                    }
                }
            }

            return Ok(trips); //zwrocenie dannych o trip
        }

        [HttpGet("api/clients/{id}/trips/")]
        public IActionResult GetTripsClient(int id)
        {
            var trips = new List<object>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand checkExists =
                       new SqlCommand("SELECT COUNT(1) FROM Client WHERE IdClient = @ClientId", connection)) //sprawdzenie czy client istnieje
                {
                    checkExists.Parameters.AddWithValue("ClientId", id);
                    int count = Convert.ToInt32(checkExists.ExecuteScalar());
                    if (count == 0)
                    {
                        return NotFound("Client " + id + " not found"); //jezeli nie istnieje to notfound
                    }
                }

                using (SqlCommand command =
                       new SqlCommand(
                           "SELECT t.Name from Trip t join Client_Trip c on c.idTrip = t.idTrip join Client cl on cl.idClient = c.idKlient where cl.idClient = @ClientID",
                           connection)) //otzymanie imie tripu gdzie bierze udzial client
                {
                    command.Parameters.AddWithValue("@ClientID", id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            trips.Add(reader.GetString(0));
                        }
                    }
                }

                return Ok(trips); //zwrocenie imienia tripu gdzie bierze udzial klient

            }
        }

        [HttpPost("api/clients")]
        public IActionResult PostClient(ClientDTO client)
        {
            if (string.IsNullOrWhiteSpace(client.firstName) ||
                string.IsNullOrWhiteSpace(client.lastName) || string.IsNullOrWhiteSpace(client.phoneNumber) ||
                string.IsNullOrWhiteSpace(client.email) || string.IsNullOrWhiteSpace(client.pesel))
            { //sprawdzenie czy danne nie sa puste
                return BadRequest("All fields are required");//zwrocenie badrequest bo cos jest puste
            }

            if (!client.email.Contains("@"))
            {
                return BadRequest("Invalid email"); //bad request kiedy email nie zawiera @
            }

            if (client.pesel.Length != 11)
            {
                return BadRequest("Invalid pesel"); // sprawdzenie czy pesel ma 11 znakow
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command =
                       new SqlCommand(
                           "INSERT INTO CLIENT (FirstName, LastName, Email, Telephone, Pesel) values (@FirstName, @LastName, @Email, @Telephone, @Pesel)"))
                {
                    command.Parameters.AddWithValue("@FirstName", client.firstName);
                    command.Parameters.AddWithValue("@LastName", client.lastName);
                    command.Parameters.AddWithValue("@Email", client.email);
                    command.Parameters.AddWithValue("@Telephone", client.phoneNumber);
                    command.Parameters.AddWithValue("@Pesel", client.pesel);
                    int id = (int)command.ExecuteScalar();
                    return Created($"/api/clients/{id}", new { Id = id }); //wstawianie wszystkich dannych do nowego clienta
                }

            }


        }

        [HttpPut("api/clients/{id}/trips/{tripId}")]
        public IActionResult PutTrip(int id, int tripId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand checkClientExists =
                       new SqlCommand("SELECT COUNT(1) FROM Client WHERE IdClient = @ClientId", connection)) //sprawdzenie czy klient istnieje
                {
                    checkClientExists.Parameters.AddWithValue("ClientId", id);
                    int count = Convert.ToInt32(checkClientExists.ExecuteScalar());
                    if (count == 0)
                    {
                        return NotFound("Client " + id + " not found");
                    }
                }

                using (SqlCommand checkTripExists =
                       new SqlCommand("SELECT COUNT(1) FROM Trip WHERE IdTrip = @TripId", connection)) //sprawdzenie czy trip istnieje
                {
                    checkTripExists.Parameters.AddWithValue("TripId", tripId);
                    int count = Convert.ToInt32(checkTripExists.ExecuteScalar());
                    if (count == 0)
                    {
                        return NotFound("Trip " + tripId + " not found");
                    }
                }
               var checkMax = new SqlCommand("SELECT MaxPeople FROM Trip where idTrip = @TripId", connection);
               int maxPeople = Convert.ToInt32(checkMax.ExecuteScalar());
               var checkCurrent = new SqlCommand("SELECT Count(*) FROM Client_Trip where idTrip = @TripId", connection);
               int currentPeople = Convert.ToInt32(checkCurrent.ExecuteScalar());
               if (currentPeople > maxPeople) //sprawdzenie czmaxPeople < currentPeople;
               {
                   return BadRequest("The trip has no free slots");
               }

               using (SqlCommand command =
                      new SqlCommand(
                          "INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt) VALUES (@IdClient, @IdTrip, GETDATE())", //wstawienie dannych do clienttrip
                          connection))
               {
                   command.Parameters.AddWithValue("@IdClient", id);
                   command.Parameters.AddWithValue("@IdTrip", tripId);
                   return Ok("Client registered at " + tripId); //zwrot informacjy ze klient zostal zarejestrowany do tripa
               }
            }
        }
        [HttpDelete("api/clients/{id}/trips/{tripId}")]
        public IActionResult DeleteTrip(int id, int tripId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand checkRegistrationExistance = new SqlCommand(
                           "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @IdClient and IdTrip = @IdTrip",
                           connection)) //sprawdzenie czy istnieje taka rejestracja
                {
                    checkRegistrationExistance.Parameters.AddWithValue("@IdClient", id);
                    checkRegistrationExistance.Parameters.AddWithValue("@IdTrip", tripId);
                    int count = Convert.ToInt32(checkRegistrationExistance.ExecuteScalar());
                    if (count == 0)
                    {
                        return NotFound("Rejestracja gdzie numer clienta to " + id + " a numer tripu " + tripId + " not found");
                    } //zwrot ze nie istnieje taka rejestracja
                }

                using (SqlCommand command =
                       new SqlCommand("DELETE FROM Client_Trip where IdClient = @IdClient and IdTrip = @IdTrip"))
                {//usuwamy rejestracje
                    command.Parameters.AddWithValue("@IdClient", id);
                    command.Parameters.AddWithValue("@IdTrip", tripId);
                    return Ok("Registration deleted"); //zwrocenie informacjy ze zostala usunieta
                }
            }
        }
    }
}