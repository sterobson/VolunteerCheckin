namespace VolunteerCheckin.Functions;

public static class Constants
{
    // Partition Keys
    public const string EventPartitionKey = "EVENT";
    public const string AdminPartitionKey = "ADMIN";

    // Roles
    public const string AdminRole = "Admin";

    // GPS Settings
    public const double CheckInRadiusMeters = 100;

    // Headers
    public const string AdminEmailHeader = "X-Admin-Email";

    // Default Area
    public const string DefaultAreaName = "Unassigned";
    public const string DefaultAreaDescription = "Default area for unassigned checkpoints";

    // Check-In Methods
    public const string CheckInMethodGps = "GPS";
    public const string CheckInMethodManual = "Manual";
    public const string CheckInMethodAdmin = "Admin";

    // Error Messages
    public const string ErrorInvalidRequest = "Invalid request";
    public const string ErrorAssignmentNotFound = "Assignment not found";
    public const string ErrorLocationNotFound = "Location not found";
    public const string ErrorMarshalNotFound = "Marshal not found";
    public const string ErrorEventNotFound = "Event not found";
    public const string ErrorAreaNotFound = "Area not found";
    public const string ErrorChecklistItemNotFound = "Checklist item not found";
    public const string ErrorNotAuthorized = "Not authorized to access this event";
    public const string ErrorCannotDeleteDefaultArea = "Cannot delete the default area";
    public const string ErrorCannotRenameDefaultArea = "Cannot rename the default area";
    public const string ErrorAreaHasCheckpoints = "Cannot delete area that has checkpoints assigned to it";
    public const string ErrorCannotRemoveLastAdmin = "Cannot remove the last admin from an event";
    public const string ErrorEmailRequired = "Email is required";
    public const string ErrorMarshalIdOrNameRequired = "Either MarshalId or MarshalName must be provided";
    public const string ErrorCheckInTooFarAway = "Check-in location is too far from the checkpoint";
    public const string ErrorChecklistAlreadyCompleted = "This checklist item has already been completed";
    public const string ErrorChecklistNotAuthorizedToComplete = "You don't have permission to complete this item";

    // Checklist Scopes
    public const string ChecklistScopeEveryoneInAreas = "EveryoneInAreas";
    public const string ChecklistScopeEveryoneAtCheckpoints = "EveryoneAtCheckpoints";
    public const string ChecklistScopeSpecificPeople = "SpecificPeople";
    public const string ChecklistScopeOnePerArea = "OnePerArea";
    public const string ChecklistScopeOnePerCheckpoint = "OnePerCheckpoint";
    public const string ChecklistScopeOneLeadPerArea = "OneLeadPerArea";
    public const string ChecklistScopeEveryAreaLead = "EveryAreaLead";

    // Checklist Completion Context Types
    public const string ChecklistContextPersonal = "Personal";
    public const string ChecklistContextCheckpoint = "Checkpoint";
    public const string ChecklistContextArea = "Area";

    // Checklist Sentinel Values (for "all" matching)
    public const string AllCheckpoints = "ALL_CHECKPOINTS";
    public const string AllAreas = "ALL_AREAS";
    public const string AllMarshals = "ALL_MARSHALS";

    // Actor Types (who performed an action)
    public const string ActorTypeMarshal = "Marshal";
    public const string ActorTypeEventAdmin = "EventAdmin";
    public const string ActorTypeAreaLead = "AreaLead";
}
