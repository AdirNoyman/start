using AutoMapper;
using CityInfo.API.Models;
using CityInfo.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace CityInfo.API.Controllers
{
    [Route("api/v{version:apiVersion}/cities/{cityId}/pointsofinterest")]
    [Authorize(Policy = "MustBeFromAntwerp")]
    [ApiVersion("2.0")]
    [ApiController]
    public class PointsOfInterestController : ControllerBase
    {
        private readonly ILogger<PointsOfInterestController> _logger;
        private readonly IMailService _mailService;
        private readonly CitiesDataStore _citiesDataStore;
        private readonly IMapper _mapper;
        private readonly ICityInfoRepository _cityInfoRepository;

        public PointsOfInterestController(ILogger<PointsOfInterestController> logger,
            IMailService mailService,
            CitiesDataStore citiesDataStore, ICityInfoRepository cityInfoRepository, IMapper mapper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mailService = mailService ?? throw new ArgumentNullException(nameof(mailService));
            _cityInfoRepository = cityInfoRepository ?? throw new ArgumentNullException(nameof(cityInfoRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PointOfInterestDto>>> GetPointsOfInterest(int cityId)
        {
            // var cityName = User.Claims.FirstOrDefault(c => c.Type == "city")?.Value;

            // if (!await _cityInfoRepository.CityNameMatchesCityId(cityName, cityId))
            // {
            //     return Forbid("You are forbidden to this city 🤨");
            // }

            if (!await _cityInfoRepository.CityExistAsync(cityId))
            {
                _logger.LogInformation($"City with id {cityId} was not found when trying to get points of interest");
                return NotFound("City not found 🤨");
            }

            var pointsOfInterestForCity = await _cityInfoRepository.GetPointsOfInterestsForCityAsync(cityId);

            return Ok(_mapper.Map<IEnumerable<PointOfInterestDto>>(pointsOfInterestForCity));
        }

        [HttpGet("{pointofinterestid}", Name = "GetPointOfInterest")]
        public async Task<ActionResult<PointOfInterestDto>> GetPointOfInterest(
            int cityId, int pointOfInterestId)
        {
            if (!await _cityInfoRepository.CityExistAsync(cityId))
            {
                return NotFound($"City with id {cityId} was not found 😩");
            }

            var pointOfInterest = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);

            if (pointOfInterest == null)
            {
                return NotFound($"Point of interest with id {pointOfInterestId} not found 😩");
            }

            return Ok(_mapper.Map<PointOfInterestDto>(pointOfInterest));
        }

        [HttpPost]
        public async Task<ActionResult<PointOfInterestDto>> CreatePointOfInterest(
           int cityId,
           PointOfInterestForCreationDto pointOfInterest)
        {

            if (!await _cityInfoRepository.CityExistAsync(cityId))
            {
                return NotFound("City not found 😩");
            }

            // Dto(source) to entity(target)
            var finalPointOfInterest = _mapper.Map<Entities.PointOfInterest>(pointOfInterest);

            // Add the point of intrest to the requested city
            await _cityInfoRepository.AddPointOfInterestForCityAsync(cityId, finalPointOfInterest);

            await _cityInfoRepository.SaveChangesAsync();

            // entity(source) to Dto(tagret)
            var createdPointOfInterestToReturn = _mapper.Map<Models.PointOfInterestDto>(finalPointOfInterest);

            // Create route for getting the new point of intrest
            return CreatedAtRoute("GetPointOfInterest",
                 new
                 {
                     cityId = cityId,
                     pointOfInterestId = createdPointOfInterestToReturn.Id
                 },
                 // This is JSON object that will be returned
                 createdPointOfInterestToReturn);
        }

        [HttpPut("{pointofinterestid}")]
        public async Task<ActionResult> UpdatePointOfInterest(int cityId, int pointOfInterestId,
            PointOfInterestForUpdateDto pointOfInterest)
        {
            if (!await _cityInfoRepository.CityExistAsync(cityId))
            {
                return NotFound("City not found 😩");
            }

            // find point of interest
            var pointOfInterestEntity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
            if (pointOfInterestEntity == null)
            {
                return NotFound("PointsOfInterest not found 😩");
            }

            // take the data that came from the point of interest dto and update the point of interest entity (DB)
            _mapper.Map(pointOfInterest, pointOfInterestEntity);

            // Save (commit) the changes to the DB
            await _cityInfoRepository.SaveChangesAsync();

            return NoContent();
        }


        [HttpPatch("{pointofinterestid}")]
        public async Task<ActionResult> PartiallyUpdatePointOfInterest(
            int cityId, int pointOfInterestId,
            JsonPatchDocument<PointOfInterestForUpdateDto> patchDocument)
        {

            if (!await _cityInfoRepository.CityExistAsync(cityId))
            {

                return NotFound("City not found 😩");

            }

            var pointOfInterestEntity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);

            if (pointOfInterestEntity == null) NotFound("Point of interest not found 😩");

            var pointOfInterestToPatch = _mapper.Map<PointOfInterestForUpdateDto>(pointOfInterestEntity);

            patchDocument.ApplyTo(pointOfInterestToPatch, ModelState);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TryValidateModel(pointOfInterestToPatch))
            {
                return BadRequest(ModelState);
            }

            _mapper.Map(pointOfInterestToPatch, pointOfInterestEntity);

            // Save(commit) changes to the DB
            await _cityInfoRepository.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{pointOfInterestId}")]
        public async Task<ActionResult> DeletePointOfInterest(int cityId, int pointOfInterestId)
        {
            if (!await _cityInfoRepository.CityExistAsync(cityId))
            {

                return NotFound("City not found 😩");
            }

            var pointOfInterestEntity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);

            if (pointOfInterestEntity == null) NotFound("Point of interest not found 😩");

            _cityInfoRepository.DeletePointOfInterest(pointOfInterestEntity);

            // Save(commit) changes to the DB
            await _cityInfoRepository.SaveChangesAsync();

            _mailService.Send(
                "Point of interest deleted.",
                $"Point of interest {pointOfInterestEntity.Name} with id {pointOfInterestEntity.Id} was deleted.");
            return NoContent();
        }

    }
}
