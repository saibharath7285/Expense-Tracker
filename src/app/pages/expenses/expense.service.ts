import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Category,
  Expense,
  ExpenseMutationResponse,
  ExpenseRequest,
  SpendingSummary,
} from './expense.types';
import { environment } from '../../../environment';

@Injectable({
  providedIn: 'root',
})
export class ExpenseService {
  readonly endpoint = `${environment.api}/api/expensetracker`;

  constructor(private readonly http: HttpClient) {}

  getExpenses(): Observable<Expense[]> {
    return this.http.get<Expense[]>(`${this.endpoint}/expenses`);
  }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.endpoint}/categories`);
  }

  getSpendingSummary(): Observable<SpendingSummary[]> {
    return this.http.get<SpendingSummary[]>(`${this.endpoint}/summary`);
  }

  addExpense(expense: ExpenseRequest): Observable<ExpenseMutationResponse> {
    return this.http.post<ExpenseMutationResponse>(`${this.endpoint}/expenses`, expense);
  }

  updateExpense(id: number, expense: ExpenseRequest): Observable<ExpenseMutationResponse> {
    return this.http.put<ExpenseMutationResponse>(`${this.endpoint}/expenses/${id}`, expense);
  }

  deleteExpense(id: number): Observable<ExpenseMutationResponse> {
    return this.http.delete<ExpenseMutationResponse>(`${this.endpoint}/expenses/${id}`);
  }
}
