from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.document_snapshot import DocumentSnapshot
from ...models.problem_details import ProblemDetails
from ...models.snapshot_request import SnapshotRequest
from typing import cast



def _get_kwargs(
    *,
    body: SnapshotRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/library/snapshots/restore",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> DocumentSnapshot | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = DocumentSnapshot.from_dict(response.json())



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


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[DocumentSnapshot | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: SnapshotRequest,

) -> Response[DocumentSnapshot | ProblemDetails]:
    """ Puts a closed document back as a snapshot holds it, after keeping a snapshot of how it stood.

    Args:
        body (SnapshotRequest): Puts a document back as a snapshot holds it, or forgets a
            snapshot.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[DocumentSnapshot | ProblemDetails]
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
    body: SnapshotRequest,

) -> DocumentSnapshot | ProblemDetails | None:
    """ Puts a closed document back as a snapshot holds it, after keeping a snapshot of how it stood.

    Args:
        body (SnapshotRequest): Puts a document back as a snapshot holds it, or forgets a
            snapshot.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        DocumentSnapshot | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: SnapshotRequest,

) -> Response[DocumentSnapshot | ProblemDetails]:
    """ Puts a closed document back as a snapshot holds it, after keeping a snapshot of how it stood.

    Args:
        body (SnapshotRequest): Puts a document back as a snapshot holds it, or forgets a
            snapshot.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[DocumentSnapshot | ProblemDetails]
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
    body: SnapshotRequest,

) -> DocumentSnapshot | ProblemDetails | None:
    """ Puts a closed document back as a snapshot holds it, after keeping a snapshot of how it stood.

    Args:
        body (SnapshotRequest): Puts a document back as a snapshot holds it, or forgets a
            snapshot.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        DocumentSnapshot | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
