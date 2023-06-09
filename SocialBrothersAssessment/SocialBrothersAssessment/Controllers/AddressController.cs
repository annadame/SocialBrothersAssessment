﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialBrothersAssessment.Models;
using System.Collections.Specialized;
using System.Reflection;

namespace SocialBrothersAssessment.Controllers
{
    [ApiController]
    [Route("addresses")]
    public class AddressController : ControllerBase
    {
        private readonly AddressDbContext _context;
        private HttpClient _client;
        private NameValueCollection _appSettings;

        public AddressController(AddressDbContext context)
        {
            _context = context;
            _client = new HttpClient();
            _appSettings = System.Configuration.ConfigurationManager.AppSettings;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Address>>> GetAddresses(string? filter, string? orderBy)
        {
            if (_context.Addresses == null)
            {
                return NotFound();
            }

            var addresses = await _context.Addresses.ToListAsync();
            
            // Filtering by getting all fields of the object and checking for matches in each field - object pair
            if (!filter.IsNullOrEmpty())
            {
                var filteredAddresses = new List<Address>();
                FieldInfo[] addressFields = typeof(Address).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (FieldInfo field in addressFields)
                {
                    filteredAddresses.AddRange(addresses.Where(a => field.GetValue(a).ToString().Equals(filter) && !filteredAddresses.Any(f => f.Id == a.Id)));
                }

                addresses = filteredAddresses;
            }

            // Sorting possible on all fields, both ascending and descending, e.g. Country;asc
            if (!orderBy.IsNullOrEmpty())
            {
                var query = orderBy.Split(';');
                var field = typeof(Address).GetProperty(query[0]);
                bool asc = query[1].Equals("asc");

                if (field == null)
                {
                    return BadRequest($"Field {query[0]} for ordering does not exist");
                }

                if (asc) { addresses = addresses.OrderBy(field.GetValue).ToList(); }
                else { addresses = addresses.OrderByDescending(field.GetValue).ToList(); }
            }

            return addresses;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Address>> GetAddress(long id)
        {
            var address = await _context.Addresses.FindAsync(id);

            if (address == null)
            {
                return NotFound();
            }

            return address;
        }

        [HttpPost]
        public async Task<ActionResult<Address>> PostAddress(Address address)
        {
            // Set Id to 0 to ensure the use of auto increment
            address.Id = 0;

            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAddress), new { id = address.Id }, address);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutAddress(long id, Address address)
        {
            address.Id = id;
            _context.Entry(address).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException) 
            {
                if (!(_context.Addresses?.Any(e => e.Id == id)).GetValueOrDefault())
                {
                    return NotFound();
                }
                else { throw; }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAddress(long id)
        {
            var address = await _context.Addresses.FindAsync(id);
            if (address == null)
            {
                return NotFound();
            }

            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("distance")]
        public async Task<IActionResult> GetDistance(long orgId, long destId)
        {
            var org_response = await GetAddress(orgId);
            var dest_response = await GetAddress(destId);

            if (org_response.Value == null || dest_response.Value == null)
            {
                return BadRequest("One of the addresses does not exist in the database");
            }

            var origin = GetAddressText(org_response.Value);
            var destination = GetAddressText(dest_response.Value);

            var token = _appSettings["distanceMatrixAPI"];
            var path = $"https://api.distancematrix.ai/maps/api/distancematrix/json?origins={origin}&destinations={destination}&key={token}";

            HttpResponseMessage response = await _client.GetAsync(path);
            if (!response.IsSuccessStatusCode)
            {
                return BadRequest();
            }

            var distance = await response.Content.ReadAsStringAsync();
            return Ok(distance);
        }

        private string GetAddressText(Address address)
        {
            return string.Join(" ", address.Street, address.HouseNumber.ToString(), address.ZipCode, address.City, address.Country);
        }
    }
}
