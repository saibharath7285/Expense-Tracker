namespace ExpenseTracker.API.Entities.DBConstants
{
    public class StoredProcedureConstants
    {
        public const string GET_EXPENSES = "dbo.spGetExpenses";
        public const string ADD_EXPENSE = "dbo.spAddExpense";
        public const string UPDATE_EXPENSE = "dbo.spUpdateExpense";
        public const string DELETE_EXPENSE = "dbo.spDeleteExpense";
        public const string GET_CATEGORIES = "dbo.spGetCategories";
        public const string GET_SPENDING_SUMMARY = "dbo.spGetSpendingSummary";
        public const string SP_GET_EXPENSES = GET_EXPENSES;

        public const string GET_KNOWLEDGE_CHUNK_HASHES = "dbo.spGetKnowledgeChunkHashes";
        public const string INSERT_KNOWLEDGE_CHUNK = "dbo.spInsertKnowledgeChunk";
        public const string DELETE_KNOWLEDGE_CHUNK_BY_HASH = "dbo.spDeleteKnowledgeChunkByHash";
        public const string SEARCH_KNOWLEDGE_CHUNKS = "dbo.spSearchKnowledgeChunks";
    }
}
