namespace SourcingTenderManagementSystem.Web.Models.Entities;

public enum SupplierStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Suspended = 3
}

public enum PurchaseRequestStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Returned = 5,
    ConvertedToTender = 6
}

public enum TenderStatus
{
    Draft = 0,
    Published = 1,
    Open = 2,
    Closed = 3,
    UnderEvaluation = 4,
    AwaitingManagerReview = 5,
    AwaitingCommitteeDecision = 6,
    Awarded = 7,
    Cancelled = 8
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3
}

public enum BidStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Compliant = 3,
    NonCompliant = 4,
    Accepted = 5,
    Rejected = 6,
    Withdrawn = 7
}

public enum ComplianceStatus
{
    Compliant = 0,
    NonCompliant = 1,
    PendingClarification = 2
}

public enum ClarificationStatus
{
    Pending = 0,
    Answered = 1,
    Resolved = 2
}

public enum DecisionStatus
{
    RecommendationPending = 0,
    ManagerApproved = 1,
    ManagerRejected = 2,
    ReturnedForReview = 3,
    CommitteeApproved = 4,
    CommitteeRejected = 5
}

public enum EvaluationKind
{
    Technical = 0,
    Financial = 1
}

public enum ApprovalStage
{
    Manager = 0,
    TopCommittee = 1
}

public enum CommitteeKind
{
    Evaluation = 0,
    TopCommittee = 1
}
