from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.document_recovery import DocumentRecovery
from ...models.document_recovery_request import DocumentRecoveryRequest
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    *,
    body: DocumentRecoveryRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/library/recovery",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> DocumentRecovery | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = DocumentRecovery.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[DocumentRecovery | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: DocumentRecoveryRequest,

) -> Response[DocumentRecovery | ProblemDetails]:
    """ Restores or forgets autosaved work. The document has to be closed.

    Args:
        body (DocumentRecoveryRequest): What to do with autosaved work, with the document closed.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[DocumentRecovery | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: DocumentRecoveryRequest,

) -> DocumentRecovery | ProblemDetails | None:
    """ Restores or forgets autosaved work. The document has to be closed.

    Args:
        body (DocumentRecoveryRequest): What to do with autosaved work, with the document closed.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        DocumentRecovery | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: DocumentRecoveryRequest,

) -> Response[DocumentRecovery | ProblemDetails]:
    """ Restores or forgets autosaved work. The document has to be closed.

    Args:
        body (DocumentRecoveryRequest): What to do with autosaved work, with the document closed.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[DocumentRecovery | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: DocumentRecoveryRequest,

) -> DocumentRecovery | ProblemDetails | None:
    """ Restores or forgets autosaved work. The document has to be closed.

    Args:
        body (DocumentRecoveryRequest): What to do with autosaved work, with the document closed.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        DocumentRecovery | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
