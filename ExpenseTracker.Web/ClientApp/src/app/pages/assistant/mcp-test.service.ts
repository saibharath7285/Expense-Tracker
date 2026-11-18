import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environment';

export interface McpToolResponse {
  success: boolean;
  output: string;
}

@Injectable({
  providedIn: 'root',
})
export class McpTestService {
  private readonly endpoint = `${environment.api}/api/mcp/tools`;

  constructor(private readonly http: HttpClient) {}

  callTool(
    toolName: string,
    argumentsValue: Record<string, unknown>,
  ): Observable<McpToolResponse> {
    return this.http.post<McpToolResponse>(this.endpoint, {
      toolName,
      arguments: argumentsValue,
    });
  }
}
