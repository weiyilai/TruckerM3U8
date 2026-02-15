using Microsoft.AspNetCore.SignalR;
using SCSSdkClient;
using SCSSdkClient.Object;
using TruckerM3U8.Hubs;

namespace TruckerM3U8.Services;

/// <summary>
/// Background service that reads SCS telemetry from shared memory
/// and broadcasts it to SignalR clients.
/// </summary>
public class TelemetryService : BackgroundService
{
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<TelemetryService> _logger;
    private SCSSdkTelemetry? _telemetry;

    public TelemetryService(IHubContext<TelemetryHub> hubContext, ILogger<TelemetryService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelemetryService starting...");

        _telemetry = new SCSSdkTelemetry();
        _telemetry.Data += OnTelemetryData;

        if (_telemetry.Error != null)
        {
            _logger.LogWarning("SCS SDK not hooked (game may not be running): {Error}", _telemetry.Error.Message);
        }
        else
        {
            _logger.LogInformation("SCS SDK telemetry hooked successfully.");
        }

        // Keep the service alive until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private void OnTelemetryData(SCSTelemetry data, bool newTimestamp)
    {
        if (!newTimestamp)
            return;

        try
        {
            var payload = MapTelemetry(data);
            _hubContext.Clients.All.SendAsync("ReceiveTelemetry", payload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting telemetry data");
        }
    }

    /// <summary>
    /// Maps the full SCSTelemetry object into a flat anonymous object for JSON serialization.
    /// </summary>
    private static object MapTelemetry(SCSTelemetry t)
    {
        var trailer0 = t.TrailerValues?.Length > 0 ? t.TrailerValues[0] : null;

        return new
        {
            // --- General ---
            SdkActive = t.SdkActive,
            Paused = t.Paused,
            Timestamp = t.Timestamp,
            SimulationTimestamp = t.SimulationTimestamp,
            RenderTimestamp = t.RenderTimestamp,
            Game = t.Game.ToString(),
            GameVersion = $"{t.GameVersion.Major}.{t.GameVersion.Minor}",
            TelemetryVersion = $"{t.TelemetryVersion.Major}.{t.TelemetryVersion.Minor}",
            DllVersion = t.DllVersion,
            MaxTrailerCount = t.MaxTrailerCount,

            // --- Common ---
            GameTime = t.CommonValues.GameTime.Value,
            NextRestStop = t.CommonValues.NextRestStop,
            Scale = t.CommonValues.Scale,

            // --- Truck Info ---
            TruckBrand = t.TruckValues.ConstantsValues.Brand,
            TruckBrandId = t.TruckValues.ConstantsValues.BrandId,
            TruckName = t.TruckValues.ConstantsValues.Name,
            TruckId = t.TruckValues.ConstantsValues.Id,
            LicensePlate = t.TruckValues.ConstantsValues.LicensePlate,
            LicensePlateCountry = t.TruckValues.ConstantsValues.LicensePlateCountry,
            LicensePlateCountryId = t.TruckValues.ConstantsValues.LicensePlateCountryId,
            ShifterType = t.TruckValues.ConstantsValues.MotorValues.ShifterTypeValue.ToString(),

            // --- Dashboard ---
            Speed = t.TruckValues.CurrentValues.DashboardValues.Speed.Value,
            SpeedKph = t.TruckValues.CurrentValues.DashboardValues.Speed.Kph,
            SpeedMph = t.TruckValues.CurrentValues.DashboardValues.Speed.Mph,
            RPM = t.TruckValues.CurrentValues.DashboardValues.RPM,
            CruiseControlSpeed = t.TruckValues.CurrentValues.DashboardValues.CruiseControlSpeed.Value,
            CruiseControlSpeedKph = t.TruckValues.CurrentValues.DashboardValues.CruiseControlSpeed.Kph,
            CruiseControlActive = t.TruckValues.CurrentValues.DashboardValues.CruiseControl,
            Odometer = t.TruckValues.CurrentValues.DashboardValues.Odometer,
            Wipers = t.TruckValues.CurrentValues.DashboardValues.Wipers,

            // --- Fuel ---
            FuelAmount = t.TruckValues.CurrentValues.DashboardValues.FuelValue.Amount,
            FuelCapacity = t.TruckValues.ConstantsValues.CapacityValues.Fuel,
            FuelAverageConsumption = t.TruckValues.CurrentValues.DashboardValues.FuelValue.AverageConsumption,
            FuelRange = t.TruckValues.CurrentValues.DashboardValues.FuelValue.Range,
            AdBlue = t.TruckValues.CurrentValues.DashboardValues.AdBlue,
            AdBlueCapacity = t.TruckValues.ConstantsValues.CapacityValues.AdBlue,

            // --- Engine / Transmission ---
            GearSelected = t.TruckValues.CurrentValues.MotorValues.GearValues.Selected,
            GearDashboard = t.TruckValues.CurrentValues.DashboardValues.GearDashboards,
            HShifterSlot = t.TruckValues.CurrentValues.MotorValues.GearValues.HShifterSlot,
            ForwardGearCount = t.TruckValues.ConstantsValues.MotorValues.ForwardGearCount,
            ReverseGearCount = t.TruckValues.ConstantsValues.MotorValues.ReverseGearCount,
            EngineRpmMax = t.TruckValues.ConstantsValues.MotorValues.EngineRpmMax,
            DifferentialRatio = t.TruckValues.ConstantsValues.MotorValues.DifferentialRation,
            ElectricEnabled = t.TruckValues.CurrentValues.ElectricEnabled,
            EngineEnabled = t.TruckValues.CurrentValues.EngineEnabled,
            RetarderLevel = t.TruckValues.CurrentValues.MotorValues.BrakeValues.RetarderLevel,
            RetarderStepCount = t.TruckValues.ConstantsValues.MotorValues.RetarderStepCount,

            // --- Brakes ---
            ParkingBrake = t.TruckValues.CurrentValues.MotorValues.BrakeValues.ParkingBrake,
            MotorBrake = t.TruckValues.CurrentValues.MotorValues.BrakeValues.MotorBrake,
            AirPressure = t.TruckValues.CurrentValues.MotorValues.BrakeValues.AirPressure,
            BrakeTemperature = t.TruckValues.CurrentValues.MotorValues.BrakeValues.Temperature,

            // --- Temperatures / Pressures ---
            OilPressure = t.TruckValues.CurrentValues.DashboardValues.OilPressure,
            OilTemperature = t.TruckValues.CurrentValues.DashboardValues.OilTemperature,
            WaterTemperature = t.TruckValues.CurrentValues.DashboardValues.WaterTemperature,
            BatteryVoltage = t.TruckValues.CurrentValues.DashboardValues.BatteryVoltage,

            // --- Warnings ---
            WarningAirPressure = t.TruckValues.CurrentValues.DashboardValues.WarningValues.AirPressure,
            WarningAirPressureEmergency = t.TruckValues.CurrentValues.DashboardValues.WarningValues.AirPressureEmergency,
            WarningFuel = t.TruckValues.CurrentValues.DashboardValues.WarningValues.FuelW,
            WarningAdBlue = t.TruckValues.CurrentValues.DashboardValues.WarningValues.AdBlue,
            WarningOilPressure = t.TruckValues.CurrentValues.DashboardValues.WarningValues.OilPressure,
            WarningWaterTemperature = t.TruckValues.CurrentValues.DashboardValues.WarningValues.WaterTemperature,
            WarningBatteryVoltage = t.TruckValues.CurrentValues.DashboardValues.WarningValues.BatteryVoltage,

            // --- Lights ---
            BlinkerLeftActive = t.TruckValues.CurrentValues.LightsValues.BlinkerLeftActive,
            BlinkerRightActive = t.TruckValues.CurrentValues.LightsValues.BlinkerRightActive,
            BlinkerLeftOn = t.TruckValues.CurrentValues.LightsValues.BlinkerLeftOn,
            BlinkerRightOn = t.TruckValues.CurrentValues.LightsValues.BlinkerRightOn,
            ParkingLights = t.TruckValues.CurrentValues.LightsValues.Parking,
            BeamLow = t.TruckValues.CurrentValues.LightsValues.BeamLow,
            BeamHigh = t.TruckValues.CurrentValues.LightsValues.BeamHigh,
            Beacon = t.TruckValues.CurrentValues.LightsValues.Beacon,
            BrakeLights = t.TruckValues.CurrentValues.LightsValues.Brake,
            ReverseLights = t.TruckValues.CurrentValues.LightsValues.Reverse,
            HazardWarningLights = t.TruckValues.CurrentValues.LightsValues.HazardWarningLights,
            AuxFront = t.TruckValues.CurrentValues.LightsValues.AuxFront.ToString(),
            AuxRoof = t.TruckValues.CurrentValues.LightsValues.AuxRoof.ToString(),
            DashboardBacklight = t.TruckValues.CurrentValues.LightsValues.DashboardBacklight,

            // --- Damage ---
            DamageEngine = t.TruckValues.CurrentValues.DamageValues.Engine,
            DamageTransmission = t.TruckValues.CurrentValues.DamageValues.Transmission,
            DamageCabin = t.TruckValues.CurrentValues.DamageValues.Cabin,
            DamageChassis = t.TruckValues.CurrentValues.DamageValues.Chassis,
            DamageWheels = t.TruckValues.CurrentValues.DamageValues.WheelsAvg,

            // --- Input (raw user) ---
            InputSteering = t.ControlValues.InputValues.Steering,
            InputThrottle = t.ControlValues.InputValues.Throttle,
            InputBrake = t.ControlValues.InputValues.Brake,
            InputClutch = t.ControlValues.InputValues.Clutch,

            // --- Game Control (post-interpolation, used by simulation) ---
            GameSteering = t.ControlValues.GameValues.Steering,
            GameThrottle = t.ControlValues.GameValues.Throttle,
            GameBrake = t.ControlValues.GameValues.Brake,
            GameClutch = t.ControlValues.GameValues.Clutch,

            // --- Navigation ---
            NavigationDistance = t.NavigationValues.NavigationDistance,
            NavigationTime = t.NavigationValues.NavigationTime,
            SpeedLimit = t.NavigationValues.SpeedLimit.Value,
            SpeedLimitKph = t.NavigationValues.SpeedLimit.Kph,
            SpeedLimitMph = t.NavigationValues.SpeedLimit.Mph,

            // --- Job ---
            JobIncome = t.JobValues.Income,
            JobMarket = t.JobValues.Market.ToString(),
            CargoName = t.JobValues.CargoValues.Name,
            CargoId = t.JobValues.CargoValues.Id,
            CargoMass = t.JobValues.CargoValues.Mass,
            CargoUnitCount = t.JobValues.CargoValues.UnitCount,
            CargoUnitMass = t.JobValues.CargoValues.UnitMass,
            CargoDamage = t.JobValues.CargoValues.CargoDamage,
            CargoLoaded = t.JobValues.CargoLoaded,
            IsSpecialJob = t.JobValues.SpecialJob,
            CitySource = t.JobValues.CitySource,
            CitySourceId = t.JobValues.CitySourceId,
            CityDestination = t.JobValues.CityDestination,
            CityDestinationId = t.JobValues.CityDestinationId,
            CompanySource = t.JobValues.CompanySource,
            CompanySourceId = t.JobValues.CompanySourceId,
            CompanyDestination = t.JobValues.CompanyDestination,
            CompanyDestinationId = t.JobValues.CompanyDestinationId,
            PlannedDistanceKm = t.JobValues.PlannedDistanceKm,
            RemainingDeliveryTime = t.JobValues.RemainingDeliveryTime.Value,

            // --- Truck Position & Orientation ---
            PositionX = t.TruckValues.CurrentValues.PositionValue.Position.X,
            PositionY = t.TruckValues.CurrentValues.PositionValue.Position.Y,
            PositionZ = t.TruckValues.CurrentValues.PositionValue.Position.Z,
            OrientationHeading = t.TruckValues.CurrentValues.PositionValue.Orientation.Heading,
            OrientationPitch = t.TruckValues.CurrentValues.PositionValue.Orientation.Pitch,
            OrientationRoll = t.TruckValues.CurrentValues.PositionValue.Orientation.Roll,

            // --- Acceleration ---
            LinearVelocityX = t.TruckValues.CurrentValues.AccelerationValues.LinearVelocity.X,
            LinearVelocityY = t.TruckValues.CurrentValues.AccelerationValues.LinearVelocity.Y,
            LinearVelocityZ = t.TruckValues.CurrentValues.AccelerationValues.LinearVelocity.Z,
            LinearAccelX = t.TruckValues.CurrentValues.AccelerationValues.LinearAcceleration.X,
            LinearAccelY = t.TruckValues.CurrentValues.AccelerationValues.LinearAcceleration.Y,
            LinearAccelZ = t.TruckValues.CurrentValues.AccelerationValues.LinearAcceleration.Z,

            // --- Special Events (flags) ---
            OnJob = t.SpecialEventsValues.OnJob,
            JobFinished = t.SpecialEventsValues.JobFinished,
            JobCancelled = t.SpecialEventsValues.JobCancelled,
            JobDelivered = t.SpecialEventsValues.JobDelivered,
            Fined = t.SpecialEventsValues.Fined,
            Tollgate = t.SpecialEventsValues.Tollgate,
            Ferry = t.SpecialEventsValues.Ferry,
            Train = t.SpecialEventsValues.Train,
            Refuel = t.SpecialEventsValues.Refuel,
            RefuelPayed = t.SpecialEventsValues.RefuelPayed,

            // --- Gameplay Events (details) ---
            FinedAmount = t.GamePlay.FinedEvent.Amount,
            FinedOffence = t.GamePlay.FinedEvent.Offence.ToString(),
            TollgatePayAmount = t.GamePlay.TollgateEvent.PayAmount,
            FerryPayAmount = t.GamePlay.FerryEvent.PayAmount,
            FerrySourceName = t.GamePlay.FerryEvent.SourceName,
            FerryTargetName = t.GamePlay.FerryEvent.TargetName,
            TrainPayAmount = t.GamePlay.TrainEvent.PayAmount,
            TrainSourceName = t.GamePlay.TrainEvent.SourceName,
            TrainTargetName = t.GamePlay.TrainEvent.TargetName,
            RefuelAmount = t.GamePlay.RefuelEvent.Amount,
            DeliveredRevenue = t.GamePlay.JobDelivered.Revenue,
            DeliveredEarnedXp = t.GamePlay.JobDelivered.EarnedXp,
            DeliveredDistanceKm = t.GamePlay.JobDelivered.DistanceKm,
            DeliveredCargoDamage = t.GamePlay.JobDelivered.CargoDamage,
            DeliveredAutoParked = t.GamePlay.JobDelivered.AutoParked,
            DeliveredAutoLoaded = t.GamePlay.JobDelivered.AutoLoaded,
            CancelledPenalty = t.GamePlay.JobCancelled.Penalty,

            // --- Axle ---
            DifferentialLock = t.TruckValues.CurrentValues.DifferentialLock,
            LiftAxle = t.TruckValues.CurrentValues.LiftAxle,
            LiftAxleIndicator = t.TruckValues.CurrentValues.LiftAxleIndicator,
            TrailerLiftAxle = t.TruckValues.CurrentValues.TrailerLiftAxle,
            TrailerLiftAxleIndicator = t.TruckValues.CurrentValues.TrailerLiftAxleIndicator,

            // --- First Trailer (summary) ---
            Trailer0Attached = trailer0?.Attached ?? false,
            Trailer0Name = trailer0?.Name,
            Trailer0Brand = trailer0?.Brand,
            Trailer0BodyType = trailer0?.BodyType,
            Trailer0ChainType = trailer0?.ChainType,
            Trailer0LicensePlate = trailer0?.LicensePlate,
            Trailer0LicensePlateCountry = trailer0?.LicensePlateCountry,
            Trailer0DamageBody = trailer0?.DamageValues.Body ?? 0f,
            Trailer0DamageChassis = trailer0?.DamageValues.Chassis ?? 0f,
            Trailer0DamageWheels = trailer0?.DamageValues.Wheels ?? 0f,
            Trailer0DamageCargo = trailer0?.DamageValues.Cargo ?? 0f,
        };
    }

    public override void Dispose()
    {
        _telemetry?.Dispose();
        base.Dispose();
    }
}
