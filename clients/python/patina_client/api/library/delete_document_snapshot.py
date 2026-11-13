from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.document_snapshot_list import DocumentSnapshotList
from ...models.problem_details import ProblemDetails
from typing import cast
from uuid import UUID



def _get_kwargs(
    *,
    path: str,
    id: UUID,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["path"] = path

    json_id = str(id)
    params["id"] = json_id


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "delete",
        "url": "/api/v1/library/snapshots",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> DocumentSnapshotList | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = DocumentSnapshotList.from_dict(response.json())



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


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[DocumentSnapshotList | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    id: UUID,

) -> Response[DocumentSnapshotList | ProblemDetails]:
    """ Forgets a snapshot. The document is not changed.

    Args:
        path (str):
        id (UUID):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[DocumentSnapshotList | ProblemDetails]
     """


    kwargs = _get_kwargs(
        path=path,
id=id,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    id: UUID,

) -> DocumentSnapshotList | ProblemDetails | None:
    """ Forgets a snapshot. The document is not changed.

    Args:
        path (str):
        id (UUID):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        DocumentSnapshotList | ProblemDetails
     """


    return sync_detailed(
        client=client,
path=path,
id=id,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    id: UUID,

) -> Response[DocumentSnapshotList | ProblemDetails]:
    """ Forgets a snapshot. The document is not changed.

    Args:
        path (str):
        id (UUID):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[DocumentSnapshotList | ProblemDetails]
     """


    kwargs = _get_kwargs(
        path=path,
id=id,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    id: UUID,

) -> DocumentSnapshotList | ProblemDetails | None:
    """ Forgets a snapshot. The document is not changed.

    Args:
        path (str):
        id (UUID):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        DocumentSnapshotList | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
path=path,
id=id,

    )).parsed
