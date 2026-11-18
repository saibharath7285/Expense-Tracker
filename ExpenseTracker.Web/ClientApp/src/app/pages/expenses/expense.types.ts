export interface Expense {
  id: number;
  title: string;
  categoryId: number;
  categoryName: string;
  subCategoryId: number;
  subCategoryName: string;
  amount: number;
  expenseDate: string;
  notes?: string;
  createDateTime?: string;
}

export interface Category {
  categoryId: number;
  categoryName: string;
  subCategoryId: number | null;
  subCategoryName: string | null;
}

export interface SpendingSummary {
  categoryName: string;
  expenseCount: number;
  totalAmount: number;
}

export interface ExpenseRequest {
  title: string;
  categoryId: number;
  subCategoryId: number;
  amount: number;
  expenseDate: string;
  notes?: string | null;
}

export interface ExpenseMutationResponse {
  id?: number;
  message: string;
}
